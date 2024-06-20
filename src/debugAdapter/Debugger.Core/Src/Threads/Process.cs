// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="David Srbecký" email="dsrbecky@gmail.com"/>
//     <version>$Revision: 2285 $</version>
// </file>

using System;
using System.Collections.Generic;
using Microsoft.Win32.SafeHandles;
using Debugger.Wrappers.CorDebug;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.IO;
using System.Text;

namespace Debugger
{
			[StructLayout(LayoutKind.Sequential, CharSet=CharSet.Auto, Pack=8), ComVisible(false)]
public class STARTUPINFO
{
  public int cb;
  public string lpReserved;
  public string lpDesktop;
  public string lpTitle;
  public int dwX;
  public int dwY;
  public int dwXSize;
  public int dwYSize;
  public int dwXCountChars;
  public int dwYCountChars;
  public int dwFillAttribute;
  public int dwFlags;
  public short wShowWindow;
  public short cbReserved2;
  public IntPtr lpReserved2;
  public SafeFileHandle hStdInput;
  public SafeFileHandle hStdOutput;
  public SafeFileHandle hStdError;
}

public delegate void DataReceivedEventHandler(object sender, DataReceivedEventArgs e);

	public partial class Process: DebuggerObject, IExpirable
	{
		NDebugger debugger;
		
		ICorDebugProcess corProcess;
		ManagedCallback callbackInterface;
		
		Thread selectedThread;
		PauseSession pauseSession;
		
		bool hasExpired = false;
		
		public event EventHandler Expired;
		public event DataReceivedEventHandler OutputDataReceived;
		public event DataReceivedEventHandler ErrorDataReceived;
		
		public bool HasExpired {
			get {
				return hasExpired;
			}
		}
		
		private StreamWriter standardInput;
		
		public StreamWriter StandardInput
		{
			get
			{
				return standardInput;
			}
			set
			{
				standardInput = value;
			}
		}
		
		private StreamReader standardOutput;
		public StreamReader StandardOutput
		{
			get
			{
				return standardOutput;
			}
			set
			{
				standardOutput = value;
			}
		}
		
		private StreamReader standardError;
		public StreamReader StandardError
		{
			get
			{
				return standardError;
			}
			set
			{
				standardError = value;
			}
		}
		
		internal void NotifyHasExpired()
		{
			if(!hasExpired) {
				hasExpired = true;
				if (Expired != null) {
					Expired(this, new ProcessEventArgs(this));
				}
//				if (PausedReason == PausedReason.Exception) {
//					ExceptionEventArgs args = new ExceptionEventArgs(this, SelectedThread.CurrentException);
//					OnExceptionThrown(args);
////					if (args.Continue) {
////						this.Continue();
////					}
//				}
				debugger.RemoveProcess(this);
			}
		}
		
		/// <summary>
		/// Indentification of the current debugger session. This value changes whenever debugger is continued
		/// </summary>
		public PauseSession PauseSession {
			get {
				return pauseSession;
			}
		}
		
		public void NotifyPaused(PauseSession pauseSession)
		{
			this.pauseSession = pauseSession;
		}
		
		public NDebugger Debugger {
			get {
				return debugger;
			}
		}
		
		internal ManagedCallback CallbackInterface {
			get {
				return callbackInterface;
			}
		}
		
		internal Process(NDebugger debugger, ICorDebugProcess corProcess)
		{
			this.debugger = debugger;
			this.corProcess = corProcess;
			
			this.callbackInterface = new ManagedCallback(this);
		}

		internal ICorDebugProcess CorProcess {
			get {
				return corProcess;
			}
		}
		
		public uint Id {
			get {
				return corProcess.ID;
			}
		}
		public Thread SelectedThread {
			get {
				return selectedThread;
			}
			set {
				selectedThread = value;
			}
		}
		
		static public Process CreateProcess(NDebugger debugger, string filename, string workingDirectory, string arguments)
		{
			return debugger.MTA2STA.Call<Process>(delegate{
			                                      	return StartInternal(debugger, filename, workingDirectory, arguments);
			                                      });
		}
		
		public struct SECURITY_ATTRIBUTES1
		{
			public uint nLength;
			public IntPtr lpSecurityDescriptor;
			[MarshalAs(UnmanagedType.Bool)]
			public bool bInheritHandle;
		}
		private static void CreatePipeWithSecurityAttributes(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, SECURITY_ATTRIBUTES lpPipeAttributes, int nSize)
		{
			SafeFileHandle readHandle = null;
			SafeFileHandle writeHandle = null;
			Debugger.Interop.CorDebug._SECURITY_ATTRIBUTES attributes = _SECURITY_ATTRIBUTES.Default;

			attributes.nLength = (uint)Marshal.SizeOf(attributes);
		  if ((!CreatePipe(out hReadPipe, out hWritePipe, attributes, nSize) || hReadPipe.IsInvalid) || hWritePipe.IsInvalid)
		  {
		    throw new Win32Exception();
		  }
		}

 		[DllImport("kernel32.dll", CharSet=CharSet.Auto, SetLastError=true)]
private static extern bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, Debugger.Interop.CorDebug._SECURITY_ATTRIBUTES lpPipeAttributes, int nSize);
 
 [DllImport("kernel32.dll", CharSet=CharSet.Auto, SetLastError=true)]
private static extern int GetConsoleCP();

[DllImport("kernel32.dll", CharSet=CharSet.Auto, SetLastError=true)]
private static extern int GetConsoleOutputCP();
 
 [DllImport("kernel32.dll", CharSet=CharSet.Ansi, SetLastError=true)]
private static extern IntPtr GetCurrentProcess();

[DllImport("kernel32.dll", CharSet=CharSet.Ansi, SetLastError=true)]
private static extern bool DuplicateHandle(HandleRef hSourceProcessHandle, SafeHandle hSourceHandle, HandleRef hTargetProcess, out SafeFileHandle targetHandle, int dwDesiredAccess, bool bInheritHandle, int dwOptions);
 

 [StructLayout(LayoutKind.Sequential)]
internal class SECURITY_ATTRIBUTES
{
  public int nLength;
  public int lpSecurityDescriptor;
  public bool bInheritHandle;
}


		private static void CreatePipe(out SafeFileHandle parentHandle, out SafeFileHandle childHandle, bool parentInputs)
		{
  			SECURITY_ATTRIBUTES lpPipeAttributes = new SECURITY_ATTRIBUTES();
  			lpPipeAttributes.bInheritHandle = true;
  			SafeFileHandle hWritePipe = null;
  			parentHandle = null;
  			try
  			{
    			if (parentInputs)
    			{
      				CreatePipeWithSecurityAttributes(out childHandle, out hWritePipe, lpPipeAttributes, 0);
    				parentHandle = hWritePipe;
    			}
    			else
    			{
      				CreatePipeWithSecurityAttributes(out hWritePipe, out childHandle, lpPipeAttributes, 0);
      				parentHandle = hWritePipe;
    			}
//    			if (!DuplicateHandle(new HandleRef(this, GetCurrentProcess()), hWritePipe, new HandleRef(this, GetCurrentProcess()), out parentHandle, 0, false, 2))
//    			{
//      				throw new Win32Exception();
//    			}
  			}
  			finally
 	 		{
//    			if ((hWritePipe != null) && !hWritePipe.IsInvalid)
//    			{
//      				hWritePipe.Close();
//    			}
  			}
		}




		static unsafe Process StartInternal(NDebugger debugger, string filename, string workingDirectory,
			string arguments)
		{
			debugger.TraceMessage("Executing " + filename);
			uint[] numArray1 = new uint[17];
			numArray1[0] = 68U;
			uint[] numArray2 = new uint[4];
			STARTUPINFO startupinfo = new STARTUPINFO();
			startupinfo.cb = Marshal.SizeOf((object)startupinfo);
			new Process.SECURITY_ATTRIBUTES().bInheritHandle = true;
			SafeFileHandle parentHandle1;
			SafeFileHandle childHandle1;
			Process.CreatePipe(out parentHandle1, out childHandle1, true);
			SafeFileHandle parentHandle2;
			SafeFileHandle childHandle2;
			Process.CreatePipe(out parentHandle2, out childHandle2, false);
			SafeFileHandle parentHandle3;
			SafeFileHandle childHandle3;
			Process.CreatePipe(out parentHandle3, out childHandle3, false);
			startupinfo.hStdInput = childHandle1;
			startupinfo.hStdOutput = childHandle2;
			startupinfo.hStdError = childHandle3;
			StreamWriter streamWriter =
				new StreamWriter((Stream)new FileStream(parentHandle1, FileAccess.Write, 4096, false),
					Encoding.GetEncoding(Process.GetConsoleCP()), 4096);
			streamWriter.AutoFlush = true;
			Encoding encoding = Encoding.GetEncoding(Process.GetConsoleOutputCP());
			StreamReader streamReader1 =
				new StreamReader((Stream)new FileStream(parentHandle2, FileAccess.Read, 4096, false), encoding, true,
					4096);
			StreamReader streamReader2 =
				new StreamReader((Stream)new FileStream(parentHandle3, FileAccess.Read, 4096, false), encoding, true,
					4096);
			if (workingDirectory == null || workingDirectory == "")
				workingDirectory = Path.GetDirectoryName(filename);
			uint[] numArray3 = numArray1;
			//uint* numPtr = numArray1 == null || numArray3.Length == 0 ? (uint*) null : &numArray3[0];
			ICorDebugProcess process;
			workingDirectory = "C:\\Users\\max\\Downloads\\kkkkkk\\vscode-mock-debug\\bin\\Debug";
			filename = "test.exe";
			try
			{
				fixed (uint* lpProcessInformation = numArray2)
					process = debugger.CorDebug.CreateProcess(filename, " " + arguments,
						ref _SECURITY_ATTRIBUTES.Default, ref _SECURITY_ATTRIBUTES.Default, 1, 134217728U, IntPtr.Zero,
						workingDirectory, startupinfo, (uint)lpProcessInformation,
						CorDebugCreateProcessFlags.DEBUG_NO_SPECIAL_OPTIONS);
				return new Process(debugger, process)
				{
					StandardInput = streamWriter,
					StandardOutput = streamReader1,
					StandardError = streamReader2
				};
			}
			catch (SystemException ex)
			{
				int r = 4;
			}
			finally
			{
				int r = 3;
			}

		numArray3 = (uint[]) null;
		return null;
		}
		
		public void Break()
		{
			AssertRunning();
			
			corProcess.Stop(100); // TODO: Hardcoded value
			
			pauseSession = new PauseSession(PausedReason.ForcedBreak);

			// TODO: Code duplication from enter callback
			// Remove expired threads and functions
			foreach(Thread thread in this.Threads) {
				thread.CheckExpiration();
			}
			
			Pause(true);
		}
		
		public void Continue()
		{
			try
			{
			AssertPaused();
			
			
			pauseSession.NotifyHasExpired();
			pauseSession = null;
			OnDebuggingResumed();
			
			corProcess.Continue(0);
			}
			catch(System.Exception e)
			{
				
			}
		}
		
		public void Terminate()
		{
			// Resume stoped tread
			if (this.IsPaused) {
			// We might get more callbacks so we should maintain consistent sate
				this.Continue(); // TODO: Remove this...
			}
			
			// Expose race condition - drain callback queue
			System.Threading.Thread.Sleep(0);
			
			// Stop&terminate - both must be called
			corProcess.Stop(100); // TODO: ...and this
			corProcess.Terminate(0);
		}

		public bool IsRunning { 
			get {
				return pauseSession == null;
			}
		}
		
		public bool IsPaused {
			get {
				return !IsRunning;
			}
		}
		
		public void AssertPaused()
		{
			if (IsRunning) {
				throw new DebuggerException("Process is not paused.");
			}
		}
		
		public void AssertRunning()
		{
			if (IsPaused) {
				throw new DebuggerException("Process is not running.");
			}
		}
		
		
		public string DebuggeeVersion {
			get {
				return debugger.DebuggeeVersion;
			}
		}
		
		/// <summary>
		/// Fired when System.Diagnostics.Trace.WriteLine() is called in debuged process
		/// </summary>
		public event EventHandler<MessageEventArgs> LogMessage;
		
		protected internal virtual void OnLogMessage(MessageEventArgs arg)
		{
			TraceMessage ("Debugger event: OnLogMessage");
			if (LogMessage != null) {
				LogMessage(this, arg);
			}
		}
		
		public void TraceMessage(string message)
		{
			System.Diagnostics.Debug.WriteLine("Debugger:" + message);
			debugger.OnDebuggerTraceMessage(new MessageEventArgs(this, message));
		}
		
		public SourcecodeSegment NextStatement { 
			get {
				if (SelectedFunction == null || IsRunning) {
					return null;
				} else {
					return SelectedFunction.NextStatement;
				}
			}
		}
		
		public NamedValueCollection LocalVariables { 
			get {
				if (SelectedFunction == null || IsRunning) {
					return NamedValueCollection.Empty;
				} else {
					return SelectedFunction.Variables;
				}
			}
		}
		
		/// <summary> Gets value of given name which is accessible from selected function </summary>
		/// <returns> Null if not found </returns>
		public NamedValue GetValue(string name)
		{
			if (SelectedFunction == null || IsRunning) {
				return null;
			} else {
				return SelectedFunction.GetValue(name);
			}
		}
	}
}
