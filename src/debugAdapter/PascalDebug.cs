﻿
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace VSCodeDebug
{
	internal class Program
	{
		const int DEFAULT_PORT = 4711;

		private static bool trace_requests;
		private static bool trace_responses;
		static string LOG_FILE_PATH = null;

		private static void Main(string[] argv)
		{
			// using (StreamWriter writer = new StreamWriter("/Users/max/test.txt"))
			// {
			// 	// Записываем текст в файл
			// 	writer.WriteLine("sdf");
			// }
			Program.Log("ывамыва " );
			int port = -1;

			// parse command line arguments
			foreach (var a in argv) {
				switch (a) {
				case "--trace":
					trace_requests = true;
					break;
				case "--trace=response":
					trace_requests = true;
					trace_responses = true;
					break;
				case "--server":
					port = DEFAULT_PORT;
					break;
				default:
					if (a.StartsWith("--server=")) {
						if (!int.TryParse(a.Substring("--server=".Length), out port)) {
							port = DEFAULT_PORT;
						}
					}
					else if( a.StartsWith("--log-file=")) {
						LOG_FILE_PATH = a.Substring("--log-file=".Length);
					}
					break;
				}
			}

			if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("mono_debug_logfile")) == false) {
				LOG_FILE_PATH = Environment.GetEnvironmentVariable("mono_debug_logfile");
				trace_requests = true;
				trace_responses = true;
			}

			if (port > 0) {
				// TCP/IP server
				Program.Log("waiting for debug protocol on port " + port);
				RunServer(port);
			} else {
				// stdin/stdout
				Program.Log("waiting for debug protocol on stdin/stdout");
				RunSession(Console.OpenStandardInput(), Console.OpenStandardOutput());
			}
		}

		static TextWriter logFile;
		
	

		private static void RunSession(Stream inputStream, Stream outputStream)
		{
			DebugSession debugSession = new NDebugSession();
			debugSession.TRACE = trace_requests;
			debugSession.TRACE_RESPONSE = trace_responses;
			debugSession.Start(inputStream, outputStream).Wait();

			if (logFile!=null)
			{
				logFile.Flush();
				logFile.Close();
				logFile = null;
			}
		}

		private static void RunServer(int port)
		{
			TcpListener serverSocket = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
			serverSocket.Start();

			new System.Threading.Thread(() => {
				while (true) {
					var clientSocket = serverSocket.AcceptSocket();
					if (clientSocket != null) {
						Program.Log(">> accepted connection from client");

						new System.Threading.Thread(() => {
							using (var networkStream = new NetworkStream(clientSocket)) {
								try {
									RunSession(networkStream, networkStream);
								}
								catch (Exception e) {
									Console.Error.WriteLine("Exception: " + e);
								}
							}
							clientSocket.Close();
							Console.Error.WriteLine(">> client connection closed");
						}).Start();
					}
				}
			}).Start();
		}
	}
}
