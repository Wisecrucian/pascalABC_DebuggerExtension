
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Linq;
using System.Net;
using Debugger;
//using NDebug.Debugging.Client;
using NDebug.Debugging.Client;
using NDebug.Debugging.Evaluation;
//using VisualPascalABC;
using Exception = System.Exception;
//using ExpressionEvaluator = Debugger.ExpressionEvaluator;
using Process = Debugger.Process;

//using ExpressionEvaluator = VisualPascalABC.ExpressionEvaluator;

namespace VSCodeDebug
{ public enum DebugStatus
	{
		None,
		StepOver,
		StepIn
	}

	public class NDebugSession : DebugSession
	{
		private const string NDEBUG = "ndebug";
		private readonly string[] PASCAL_EXTENSIONS = new String[] {
			".pas",
			".md"
		};
		private const int MAX_CHILDREN = 100;
		private const int MAX_CONNECTION_ATTEMPTS = 10;
		private const int CONNECTION_ATTEMPT_INTERVAL = 500;

		private AutoResetEvent _resumeEvent = new AutoResetEvent(false);
		private bool _debuggeeExecuting = false;
		private readonly object _lock = new object();
		public NDebugger dbg;
		private NDebug.Debugging.Soft.SoftDebuggerSession _session;
		private volatile bool _debuggeeKilled = true;
		private Debugger.Process _process1;
		private ProcessInfo _activeProcess;
		private NDebug.Debugging.Client.StackFrame _activeFrame;
		private long _nextBreakpointId = 0;
		private SortedDictionary<long, BreakEvent> _breakpoints;
		private List<Catchpoint> _catchpoints;
		private DebuggerSessionOptions _debuggerSessionOptions;

		private System.Diagnostics.Process _process;
		private Handles<ObjectValue[]> _variableHandles;
		private Handles<NDebug.Debugging.Client.StackFrame> _frameHandles;
		private ObjectValue _exception;
		private Dictionary<int, Thread> _seenThreads = new Dictionary<int, Thread>();
		public delegate void DebugHelperActionDelegate(string FileName);
		public event DebugHelperActionDelegate Starting;
		public event DebugHelperActionDelegate Exited;

		private bool _attachMode = false;
		private bool _terminated = false;
		private bool _stderrEOF = true;
		private bool _stdoutEOF = true;
		
		private Process debuggedProcess;
		private string FileName;
		private string FullFileName;
		private string PrevFullFileName;
		private Debugger.Breakpoint brPoint;
		private Debugger.Breakpoint currentBreakpoint;
		private int CurrentLine;
		public DebugStatus Status;
		private bool MustDebug = false;
		public bool IsRunning = false;
		public string ExeFileName;
		public bool ShowDebugTabs=true;

		//public PascalABCCompiler.Parsers.IParser parser = null;
		EventHandler<EventArgs> debuggerStateEvent;
		

		private dynamic exceptionOptionsFromDap;


		public NDebugSession() : base()
		{
			_variableHandles = new Handles<ObjectValue[]>();
			_frameHandles = new Handles<NDebug.Debugging.Client.StackFrame>();
			_seenThreads = new Dictionary<int, Thread>();

			_debuggerSessionOptions = new DebuggerSessionOptions {
				EvaluationOptions = EvaluationOptions.DefaultOptions
			};

			_session = new NDebug.Debugging.Soft.SoftDebuggerSession();
			_session.Breakpoints = new BreakpointStore();

			_breakpoints = new SortedDictionary<long, BreakEvent>();
			_catchpoints = new List<Catchpoint>();
			

			//DebuggerLoggingService.CustomLogger = new CustomLogger();

			_session.ExceptionHandler = ex => {
				return true;
			};

			_session.LogWriter = (isStdErr, text) => {
			};

			_session.TargetStopped += (sender, e) => {
				Stopped();
				SendEvent(CreateStoppedEvent("step", e.Thread));
				_resumeEvent.Set();
			};

			_session.TargetHitBreakpoint += (sender, e) => {
				Stopped();
				SendEvent(CreateStoppedEvent("breakpoint", e.Thread));
				_resumeEvent.Set();
			};

			_session.TargetExceptionThrown += (sender, e) => {
				Stopped();
				var ex = DebuggerActiveException();
				if (ex != null) {
					_exception = ex.Instance;
					SendEvent(CreateStoppedEvent("exception", e.Thread, ex.Message));
				}
				_resumeEvent.Set();
			};

			_session.TargetUnhandledException += (sender, e) => {
				Stopped ();
				var ex = DebuggerActiveException();
				if (ex != null) {
					_exception = ex.Instance;
					SendEvent(CreateStoppedEvent("exception", e.Thread, ex.Message));
				}
				_resumeEvent.Set();
			};

			_session.TargetStarted += (sender, e) => {
				_activeFrame = null;
			};


			_session.TargetExited += (sender, e) => {

				DebuggerKill();

				_debuggeeKilled = true;

				Terminate("target exited");

				_resumeEvent.Set();
			};

			_session.TargetInterrupted += (sender, e) => {
				_resumeEvent.Set();
			};

			_session.TargetEvent += (sender, e) => {
			};

			_seenThreads[1] = new Thread(1, "main");

			//SendEvent(new ThreadEvent("started", 1));

			_session.TargetThreadStopped += (sender, e) => {
				int tid = (int)e.Thread.Id;
				lock (_seenThreads) {
					_seenThreads.Remove(tid);
				}
				SendEvent(new ThreadEvent("exited", tid));
			};

			_session.OutputWriter = (isStdErr, text) => {
				SendOutput(isStdErr ? "stderr" : "stdout", text);
			};

		}

		
		 public void debugProcessStarted(object sender, Debugger.ProcessEventArgs e)
        {
            IsRunning = true;
            //evaluator = new ExpressionEvaluator(e.Process, 1, FileName);
        }
		public override void Initialize(Response response, dynamic args)
		{
			OperatingSystem os = Environment.OSVersion;
			if (os.Platform != PlatformID.MacOSX && os.Platform != PlatformID.Unix && os.Platform != PlatformID.Win32NT) {
				SendErrorResponse(response, 3000, "NDebug Debug is not supported on this platform ({_platform}).", new { _platform = os.Platform.ToString() }, true, true);
				return;
			}

			SendResponse(response, new Capabilities() {
				supportsConfigurationDoneRequest = false,

				supportsFunctionBreakpoints = false,

				supportsConditionalBreakpoints = false,

				supportsEvaluateForHovers = false,

				supportsExceptionFilterOptions = true,
				exceptionBreakpointFilters = new dynamic[] {
					new { filter = "always", label = "All Exceptions", @default=false, supportsCondition=true, description="Break when an exception is thrown, even if it is caught later.",
						  conditionDescription = "Comma-separated list of exception types to break on"},
					new { filter = "uncaught", label = "Uncaught Exceptions", @default=false, supportsCondition=false, description="Breaks only on exceptions that are not handled."}
					}
			});

			// NDebug Debug is ready to accept breakpoints immediately
			SendEvent(new InitializedEvent());
		}

		public override async void Launch(Response response, dynamic args)
		{
			_attachMode = false;

			//SetExceptionBreakpoints(args.__exceptionOptions);

			string programPath = getString(args, "program");
			// if (programPath == null) {
			// 	SendErrorResponse(response, 3001, "Property 'program' is missing or empty.", null);
			// 	return;
			// }
			// programPath = ConvertClientPathToDebugger(programPath);
			// if (!File.Exists(programPath) && !Directory.Exists(programPath)) {
			// 	SendErrorResponse(response, 3002, "Program '{path}' does not exist.", new { path = programPath });
			// 	return;
			// }
			dbg = new NDebugger();
			string fileName = args["program"];
			dbg.ProcessStarted += debugProcessStarted;
			this.FileName = "test.pas";//Path.GetFileNameWithoutExtension(file_name) + ".pas";
			this.FullFileName = Path.Combine(Path.GetDirectoryName("test.exe"), this.FileName);
			this.ExeFileName = "test.exe";
			this.PrevFullFileName = FullFileName;
			if (true) brPoint = dbg.AddBreakpoint(fileName, 1);
			AssemblyHelper.LoadAssembly(fileName);
			debuggedProcess = dbg.Start(fileName, "B:\\w\\", "");
			
			dbg.BreakpointHit += (sender, e) => {
				
				Stopped(); 
				using (StreamWriter writer = new StreamWriter("C:\\test\\test1.txt", true))
				{
					writer.WriteLine("testing");
				}
				//SendEvent(CreateStoppedEvent("step", e.Thread));
				_resumeEvent.Set();
			};
			int r = 3;
			
			SendMessage(response);
		}
		
		private void SelectProcess(Debugger.Process process)
		{
			if (debuggedProcess != null)
			{
				//debuggedProcess.DebuggingPaused -= debuggedProcess_DebuggingPaused;
				//debuggedProcess.ExceptionThrown -= debuggedProcess_ExceptionThrown;
				// debuggedProcess.DebuggeeStateChanged -= debuggedProcess_DebuggeeStateChanged;
				// debuggedProcess.DebuggingResumed -= debuggedProcess_DebuggingResumed;
				// debuggedProcess.DebuggingPaused -= debuggedProcess_DebuggingPaused;
				// debuggedProcess.ExceptionThrown -= debuggedProcess_ExceptionThrown;
				// debuggedProcess.Expired -= debuggedProcess_Expired;
				//debuggedProcess.LogMessage -= debuggedProcess_logMessage;
			}
			debuggedProcess = process;
			if (debuggedProcess != null)
			{
				//debuggedProcess.DebuggingPaused += debuggedProcess_DebuggingPaused;
				//debuggedProcess.ExceptionThrown += debuggedProcess_ExceptionThrown;
				// debuggedProcess.DebuggeeStateChanged += debuggedProcess_DebuggeeStateChanged;
				// debuggedProcess.DebuggingResumed += debuggedProcess_DebuggingResumed;
				// debuggedProcess.DebuggingPaused += debuggedProcess_DebuggingPaused;
				// debuggedProcess.ExceptionThrown += debuggedProcess_ExceptionThrown;
				// debuggedProcess.Expired += debuggedProcess_Expired;
				//debuggedProcess.LogMessage += debuggedProcess_logMessage;
			}
			// JumpToCurrentLine();
			// OnProcessSelected(new ProcessEventArgs(process));
		}

		public override void Attach(Response response, dynamic args)
		{
			_attachMode = true;

			SetExceptionBreakpoints(args.__exceptionOptions);

			var host = getString(args, "address");
			if (host == null) {
				SendErrorResponse(response, 3007, "Property 'address' is missing or empty.");
				return;
			}

			// validate argument 'port'
			var port = getInt(args, "port", -1);
			if (port == -1) {
				SendErrorResponse(response, 3008, "Property 'port' is missing.");
				return;
			}

			IPAddress address = Utilities.ResolveIPAddress(host);
			if (address == null) {
				SendErrorResponse(response, 3013, "Invalid address '{address}'.", new { address = address });
				return;
			}

			Connect(address, port);

			SendResponse(response);
		}

		public override void Disconnect(Response response, dynamic args)
		{
			if (_attachMode) {

				lock (_lock) {
					if (_session != null) {
						_debuggeeExecuting = true;
						_breakpoints.Clear();
						_session.Breakpoints.Clear();
						_session.Continue();
						_session = null;
					}
				}

			} else {
				if (_process != null) {
					_process.Kill();
					_process = null;
				} else {
					PauseDebugger();
					DebuggerKill();

					while (!_debuggeeKilled) {
						System.Threading.Thread.Sleep(10);
					}
				}
			}

			SendResponse(response);
		}

		public override void Continue(Response response, dynamic args)
		{
			WaitForSuspend();
			SendResponse(response);
			lock (_lock) {
				if (_session != null && !_session.IsRunning && !_session.HasExited) {
					_session.Continue();
					_debuggeeExecuting = true;
				}
			}
		}

		public override void Next(Response response, dynamic args)
		{
			WaitForSuspend();
			SendResponse(response);
			lock (_lock) {
				if (_session != null && !_session.IsRunning && !_session.HasExited) {
					_session.NextLine();
					_debuggeeExecuting = true;
				}
			}
		}

		public override void StepIn(Response response, dynamic args)
		{
			WaitForSuspend();
			SendResponse(response);
			lock (_lock) {
				if (IsRunning) {
				}
			}
		}

		public override void StepOut(Response response, dynamic args)
		{
			WaitForSuspend();
			SendResponse(response);
			lock (_lock) {
				if (IsRunning) {
					//dbg.Processes[0].StepOut();
				}
			}
		}

		public override void Pause(Response response, dynamic args)
		{
			SendResponse(response);
			PauseDebugger();//TODO
		}

		public override void SetExceptionBreakpoints(Response response, dynamic args)
		{
			if (args.filterOptions != null)
			{
				if (_activeProcess != null)
					SetExceptionBreakpointsFromDap(args.filterOptions);
				else
					exceptionOptionsFromDap = args.filterOptions;
			}
			else
				SetExceptionBreakpoints(args.exceptionOptions);
			SendResponse(response);
		}

		public override void SetBreakpoints(Response response, dynamic args)
		{			
			
			 string path = null;
			 if (args.source != null) {
			 	string p = (string)args.source.path;
			 	if (p != null && p.Trim().Length > 0) {
			 		path = p;
			 	}
			 }
			 if (path == null) {
			 	SendErrorResponse(response, 3010, "setBreakpoints: property 'source' is empty or misformed", null, false, true);
			 	return;
			 }
			
			 FileName = args.source.name;
			path = ConvertClientPathToDebugger(path);

			var clientLines = args.breakpoints;
			
			HashSet<int> lin = new HashSet<int>();
			
			for (int i = 0; i < 1; i++) { //TODO
			
				Debugger.Breakpoint br = null;
				bool added = false;
				int line = clientLines[i].line;
				
				foreach (Debugger.Breakpoint bp in dbg.Breakpoints)
				{
				
					if (bp.SourcecodeSegment.SourceFullFilename == FileName && bp.SourcecodeSegment.StartLine == line)
					{
						added = true;
						br = bp;
					}
				}
				if (!added) {
					dbg.AddBreakpoint(FileName, 2);
				}
			}

			
			var breakpoints = new List<Breakpoint>();
			foreach (var l in clientLines) {
				breakpoints.Add(new Breakpoint(true, l.line.ToObject<int>()));
			}

			SendResponse(response, new SetBreakpointsResponseBody(breakpoints));
		}

		public override void StackTrace(Response response, dynamic args)
		{
			int maxLevels = getInt(args, "levels", 10);
			int threadReference = getInt(args, "threadId", 0);

			WaitForSuspend();
			WaitForSuspend();

			ThreadInfo thread = DebuggerActiveThread();
			if (thread.Id != threadReference) {
				thread = FindThread(threadReference);
				if (thread != null) {
					thread.SetActive();
				}
			}

			var stackFrames = new List<StackFrame>();
			int totalFrames = 0;

			var bt = thread.Backtrace;
			if (bt != null && bt.FrameCount >= 0) {

				totalFrames = bt.FrameCount;

				for (var i = 0; i < Math.Min(totalFrames, maxLevels); i++) {

					var frame = bt.GetFrame(i);

					string path = frame.SourceLocation.FileName;

					var hint = "subtle";
					Source source = null;
					if (!string.IsNullOrEmpty(path)) {
						string sourceName = Path.GetFileName(path);
						if (!string.IsNullOrEmpty(sourceName)) {
							if (File.Exists(path)) {
								source = new Source(sourceName, ConvertDebuggerPathToClient(path), 0, "normal");
								hint = "normal";
							} else {
								source = new Source(sourceName, null, 1000, "deemphasize");
							}
						}
					}

					var frameHandle = _frameHandles.Create(frame);
					string name = frame.SourceLocation.MethodName;
					int line = frame.SourceLocation.Line;
					stackFrames.Add(new StackFrame(frameHandle, name, source, ConvertDebuggerLineToClient(line), 0, hint));
				}
			}

			SendResponse(response, new StackTraceResponseBody(stackFrames, totalFrames));
		}

		public override void Source(Response response, dynamic arguments) {
			SendErrorResponse(response, 1020, "No source available");
		}

		public override void Scopes(Response response, dynamic args) {

			int frameId = getInt(args, "frameId", 0);
			var frame = _frameHandles.Get(frameId, null);

			var scopes = new List<Scope>();

			if (frame.Index == 0 && _exception != null) {
				scopes.Add(new Scope("Exception", _variableHandles.Create(new ObjectValue[] { _exception })));
			}

			var locals = new[] { frame.GetThisReference() }.Concat(frame.GetParameters()).Concat(frame.GetLocalVariables()).Where(x => x != null).ToArray();
			if (locals.Length > 0) {
				scopes.Add(new Scope("Local", _variableHandles.Create(locals)));
			}

			SendResponse(response, new ScopesResponseBody(scopes));
		}

		public override void Variables(Response response, dynamic args)
		{
			int reference = getInt(args, "variablesReference", -1);
			if (reference == -1) {
				SendErrorResponse(response, 3009, "variables: property 'variablesReference' is missing", null, false, true);
				return;
			}

			WaitForSuspend();
			var variables = new List<Variable>();

			ObjectValue[] children;
			if (_variableHandles.TryGet(reference, out children)) {
				if (children != null && children.Length > 0) {

					bool more = false;
					if (children.Length > MAX_CHILDREN) {
						children = children.Take(MAX_CHILDREN).ToArray();
						more = true;
					}

					if (children.Length < 20) {
						WaitHandle.WaitAll(children.Select(x => x.WaitHandle).ToArray());
						foreach (var v in children) {
							variables.Add(CreateVariable(v));
						}
					}
					else {
						foreach (var v in children) {
							v.WaitHandle.WaitOne();
							variables.Add(CreateVariable(v));
						}
					}

					if (more) {
						variables.Add(new Variable("...", null, null));
					}
				}
			}

			SendResponse(response, new VariablesResponseBody(variables));
		}

		public override void Threads(Response response, dynamic args)
		{
			var threads = new List<Thread>();
			var process = debuggedProcess;
			Dictionary<int, Thread> d;
			lock (_seenThreads) {
				d = new Dictionary<int, Thread>(_seenThreads);
			}
			threads = d.Values.ToList();
			SendResponse(response, new ThreadsResponseBody(threads));
		}

		public override void Evaluate(Response response, dynamic args)
		{
			string error = null;

			var expression = getString(args, "expression");
			if (expression == null) {
				error = "expression missing";
			} else {
				int frameId = getInt(args, "frameId", -1);
				var frame = _frameHandles.Get(frameId, null);
				if (frame != null && evaluator != null) {
					if (frame.ValidateExpression(expression)) {
						// RetValue val = evaluator.Evaluate(expression, false);
						//
						// if (val.err_mes != "") {
						// 	error = val.err_mes;
						// 	if (error.IndexOf("reference not available in the current evaluation context") > 0) {
						// 		error = "not available";
						// 	}
						// }
						// else {
						// 	int handle = 0;
						// 	
						// 	SendResponse(response, new EvaluateResponseBody(val.prim_val.ToString(), handle));
						// 	return;
						// }
					}
					else {
						error = "invalid expression";
					}
				}
				else {
					error = "no active stackframe";
				}
			}
			SendErrorResponse(response, 3014, "Evaluate request failed ({_reason}).", new { _reason = error } );
		}

		//---- private ------------------------------------------

		private void SetExceptionBreakpointsFromDap(dynamic exceptionOptions)
		{
			if (exceptionOptions != null) {
				var exceptions = exceptionOptions.ToObject<dynamic[]>();
				for (int i = 0; i < exceptions.Length; i++) {
					var exception = exceptions[i];

					bool caught = exception.filterId == "always" ? true : false;
					if (exception.condition != null && exception.condition != "") {
						string[] conditionNames = exception.condition.ToString().Split(',');
						foreach (var conditionName in conditionNames)
							_session.EnableException(conditionName, caught);
					}
					else {
						_session.EnableException("System.Exception", caught);
					}
				}
			}
		}
		private void SetExceptionBreakpoints(dynamic exceptionOptions)
		{
			if (exceptionOptions != null) {

				foreach (var cp in _catchpoints) {
					_session.Breakpoints.Remove(cp);
				}
				_catchpoints.Clear();

				var exceptions = exceptionOptions.ToObject<dynamic[]>();
				for (int i = 0; i < exceptions.Length; i++) {

					var exception = exceptions[i];

					string exName = null;
					string exBreakMode = exception.breakMode;

					if (exception.path != null) {
						var paths = exception.path.ToObject<dynamic[]>();
						var path = paths[0];
						if (path.names != null) {
							var names = path.names.ToObject<dynamic[]>();
							if (names.Length > 0) {
								exName = names[0];
							}
						}
					}

					if (exName != null && exBreakMode == "always") {
						_catchpoints.Add(_session.Breakpoints.AddCatchpoint(exName));
					}
				}
			}
		}

		private void SendOutput(string category, string data) {
			if (!String.IsNullOrEmpty(data)) {
				if (data[data.Length-1] != '\n') {
					data += '\n';
				}
				SendEvent(new OutputEvent(category, data));
			}
		}

		private void Terminate(string reason) {
			if (!_terminated) {

				for (int i = 0; i < 100 && (_stdoutEOF == false || _stderrEOF == false); i++) {
					System.Threading.Thread.Sleep(100);
				}

				SendEvent(new TerminatedEvent());

				_terminated = true;
				_process = null;
			}
		}

		private StoppedEvent CreateStoppedEvent(string reason, ThreadInfo ti, string text = null)
		{
			return new StoppedEvent((int)ti.Id, reason, text);
		}

		private ThreadInfo FindThread(int threadReference)
		{
			if (_activeProcess != null) {
				foreach (var t in _activeProcess.GetThreads()) {
					if (t.Id == threadReference) {
						return t;
					}
				}
			}
			return null;
		}

		private void Stopped()
		{
			_exception = null;
			_variableHandles.Reset();
			_frameHandles.Reset();
		}

		private Variable CreateVariable(ObjectValue v)
		{
			var dv = v.DisplayValue;
			if (dv.Length > 1 && dv [0] == '{' && dv [dv.Length - 1] == '}') {
				dv = dv.Substring (1, dv.Length - 2);
			}
			return new Variable(v.Name, dv, v.TypeName, v.HasChildren ? _variableHandles.Create(v.GetAllChildren()) : 0);
		}

		private bool HasNDebugExtension(string path)
		{
			foreach (var e in PASCAL_EXTENSIONS) {
				if (path.EndsWith(e)) {
					return true;
				}
			}
			return false;
		}

		private static bool getBool(dynamic container, string propertyName, bool dflt = false)
		{
			try {
				return (bool)container[propertyName];
			}
			catch (Exception) {
			}
			return dflt;
		}

		private static int getInt(dynamic container, string propertyName, int dflt = 0)
		{
			try {
				return (int)container[propertyName];
			}
			catch (Exception) {
				// ignore and return default value
			}
			return dflt;
		}

		private static string getString(dynamic args, string property, string dflt = null)
		{
			var s = (string)args[property];
			if (s == null) {
				return dflt;
			}
			s = s.Trim();
			if (s.Length == 0) {
				return dflt;
			}
			return s;
		}

		//-----------------------

		private void WaitForSuspend()
		{
			if (_debuggeeExecuting) {
				_resumeEvent.WaitOne();
				_debuggeeExecuting = false;
			}
		}

		private ThreadInfo DebuggerActiveThread()
		{
			lock (_lock) {
				return _session == null ? null : _session.ActiveThread;
			}
		}

		private Backtrace DebuggerActiveBacktrace() {
			var thr = DebuggerActiveThread();
			return thr == null ? null : thr.Backtrace;
		}

		
		private ExceptionInfo DebuggerActiveException() {
			var bt = DebuggerActiveBacktrace();
			return bt == null ? null : bt.GetFrame(0).GetException();
		}

		private void Connect(IPAddress address, int port)
		{
			lock (_lock) {

				_debuggeeKilled = false;

				
				_debuggeeExecuting = true;
			}
		}

		private void PauseDebugger()
		{
			lock (_lock) {
				
			}
		}

		private void DebuggerKill()
		{
			lock (_lock) {
				if (_session != null) {

					_debuggeeExecuting = true;

					if (!_session.HasExited)
						_session.Exit();

					_session.Dispose();
					_session = null;
				}
			}
		}
		
		public ExpressionEvaluator evaluator;
		
		// private void debugProcessStarted(object sender, ProcessEventArgs e)
		// {
		// 	IsRunning = true;
		// 	//evaluator = new ExpressionEvaluator(e.Process,workbench.VisualEnvironmentCompiler, FileName);
		// }
		 // void debugProcessExit(object sender, ProcessEventArgs e)
   //      {
   //          if (Exited != null && ExeFileName != null)
   //              Exited(ExeFileName);
   //          curPage = null;
   //          ShowDebugTabs = true;
   //          IsRunning = false;
   //          CurrentLineBookmark.Remove();
   //          RemoveGotoBreakpoints();
   //          AssemblyHelper.Unload();
   //          CloseOldToolTip();
   //          evaluator = null;
   //          //parser= null;
   //          FileName = null;
   //          handle = 0;
   //          ExeFileName = null;
   //          FullFileName = null;
   //          PrevFullFileName = null;
   //          Status = DebugStatus.None;
   //          dbg.ProcessStarted -= debugProcessStarted;
   //          dbg.ProcessExited -= debugProcessExit;
   //          dbg.BreakpointHit -= debugBreakpointHit;
   //          debuggedProcess = null;
   //          //GC.Collect();
   //          //dbg = null;
   //      }
	
	
	
	// private void debugBreakpointHit(object sender, BreakpointEventArgs e)
	// {
 //        	
	// }
	}
	
	
	public class AssemblyHelper
    {
        private static System.Reflection.Assembly a;
        private static List<System.Reflection.Assembly> ref_modules = new List<System.Reflection.Assembly>();
        private static Hashtable ns_ht = new Hashtable();
        private static Hashtable stand_types = new Hashtable(StringComparer.OrdinalIgnoreCase);
        private static List<Type> unit_types = new List<Type>();
        private static List<DebugType> unit_debug_types;/
        private static List<NDebug.Debugger.Soft.TypeMirror> unit_ndebug_types;
        private static DebugType pabc_system_type = null;

        static AssemblyHelper()
        {
        	stand_types["integer"] = typeof(int);
        	stand_types["byte"] = typeof(byte);
        	stand_types["shortint"] = typeof(sbyte);
        	stand_types["word"] = typeof(ushort);
        	stand_types["smallint"] = typeof(short);
        	stand_types["longint"] = typeof(int);
        	stand_types["longword"] = typeof(uint);
        	stand_types["int64"] = typeof(long);
        	stand_types["uint64"] = typeof(ulong);
        	stand_types["real"] = typeof(double);
        	stand_types["single"] = typeof(float);
        	stand_types["char"] = typeof(char);
        	stand_types["string"] = typeof(string);
        	stand_types["boolean"] = typeof(bool);
        	stand_types["object"] = typeof(object);
        }
        
        public static bool Is32BitAssembly()
        {
            if (!Environment.Is64BitProcess)
                return false;
            System.Reflection.PortableExecutableKinds peKind;
            System.Reflection.ImageFileMachine machine;
            a.ManifestModule.GetPEKind(out peKind, out machine);
            return peKind == System.Reflection.PortableExecutableKinds.Required32Bit;
        }

        public static void LoadAssembly(string file_name)
        {
            try
            {
                FileStream fs = File.OpenRead(file_name);
                byte[] buf = new byte[fs.Length];
                fs.Read(buf, 0, (int)fs.Length);
                fs.Close();
                a = System.Reflection.Assembly.Load(buf);
                
                Type[] tt = a.GetTypes();
                foreach (Type t in tt)
                {
                    if (t.Namespace != null)
                        ns_ht[t.Namespace] = t.Namespace;
                    if (t.Namespace == t.Name)
                    {
                        unit_types.Add(t);
                    }
                    try
                    {
                        object[] attrs = t.GetCustomAttributes(false);
                        foreach (Attribute attr in attrs)
                        {
                            Type attr_t = attr.GetType();
                            if (attr_t.Name == "$UsedNsAttr")
                            {
                                int count = (int)attr_t.GetField("count").GetValue(attr);
                                string ns = attr_t.GetField("ns").GetValue(attr) as string;
                                int j = 0;
                                for (int i = 0; i < count; i++)
                                {
                                    byte str_len = (byte)ns[j];
                                    string ns_s = ns.Substring(j + 1, str_len);
                                    ns_ht[ns_s] = ns_s;
                                    j += str_len + 1;
                                }
                                break;
                            }
                        }
                    }
                    catch
                    {

                    }
                }
            }
            catch (System.Exception e)
            {

            }
        }

       
        public static List<DebugType> GetUsesTypes(Process p, DebugType dt)
        {
        	if (unit_debug_types == null)
        	{
        		unit_debug_types = new List<DebugType>();
        		//foreach (Type t in unit_types)
        			//unit_debug_types.Add(DebugType.Create(p.GetModule(t.Assembly.ManifestModule.ScopeName),(uint)t.MetadataToken));
        	}
        	return unit_debug_types;
        }

        public static List<NDebug.Debugger.Soft.TypeMirror> GetUsesNDebugTypes(NDebug.Debugging.Soft.SoftDebuggerSession session)
        {
            if (unit_ndebug_types == null)
            {
                unit_ndebug_types = new List<NDebug.Debugger.Soft.TypeMirror>();
                foreach (var t in unit_types)
                    unit_ndebug_types.Add(session.GetType(t.FullName));
            }
            return unit_ndebug_types;
        }
        
        public static void Unload()
        {
            //if (ad != null) AppDomain.Unload(ad);
            //GC.Collect();
            ns_ht.Clear();
            unit_types.Clear();
            if (unit_debug_types != null)
            unit_debug_types.Clear();
            unit_debug_types = null;
            if (unit_ndebug_types != null)
                unit_ndebug_types.Clear();
            unit_ndebug_types = null;
        }
       
    }
	
	
	
	
}
