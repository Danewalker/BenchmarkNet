/*
 *  BenchmarkNet is a console application for testing the reliable UDP networking solutions
 *  Copyright (c) 2018 Stanislav Denisov
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a copy
 *  of this software and associated documentation files (the "Software"), to deal
 *  in the Software without restriction, including without limitation the rights
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *  copies of the Software, and to permit persons to whom the Software is
 *  furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in all
 *  copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 *  SOFTWARE.
 */

using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Net;
using System.Reflection;
using System.Runtime;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
// ENet 2.0.8 (https://github.com/nxrighthere/ENet-CSharp)
using ENet;
// UNet 1.0.0.9 (https://forum.unity.com/threads/standalone-library-binaries-aka-server-dll.526718)
using UnetServerDll;
// LiteNetLib 0.8 (https://github.com/RevenantX/LiteNetLib)
using LiteNetLib;
using LiteNetLib.Utils;
// Lidgren 1.7.0 (https://github.com/lidgren/lidgren-network-gen3)
using Lidgren.Network;
// MiniUDP 0.8.5 (https://github.com/ashoulson/MiniUDP)
using MiniUDP;
// Hazel 0.1.2 (https://github.com/DarkRiftNetworking/Hazel-Networking)
using Hazel;
using Hazel.Udp;
// Photon 4.0.29 (https://www.photonengine.com/en/OnPremise)
using ExitGames.Client.Photon;
// Neutrino 1.0 (https://github.com/Claytonious/Neutrino)
using Neutrino.Core;
using Neutrino.Core.Messages;
// DarkRift 2.2.0 (https://darkriftnetworking.com/DarkRift2)
using DarkRift;
using DarkRift.Server;
using DarkRift.Client;

namespace NX {
	public abstract class BenchmarkNet {
		// Meta
		public const string title = "BenchmarkNet";
		public const string version = "1.10";
		// Parameters
		public const string ip = "127.0.0.1";
		public static byte selectedLibrary = 0;
		public static ushort port = 0;
		public static ushort maxClients = 0;
		public static int serverTickRate = 0;
		public static int clientTickRate = 0;
		public static int sendRate = 0;
		public static int reliableMessages = 0;
		public static int unreliableMessages = 0;
		public static string message = String.Empty;
		// Status
		public static bool processActive = false;
		public static bool processCompleted = false;
		public static bool processCrashed = false;
		public static bool processFailure = false;
		public static bool processOverload = false;
		public static bool processUninitialized = true;
		// Stats
		public static volatile int serverReliableSent = 0;
		public static volatile int serverReliableReceived = 0;
		public static volatile int serverReliableBytesSent = 0;
		public static volatile int serverReliableBytesReceived = 0;
		public static volatile int serverUnreliableSent = 0;
		public static volatile int serverUnreliableReceived = 0;
		public static volatile int serverUnreliableBytesSent = 0;
		public static volatile int serverUnreliableBytesReceived = 0;
		public static volatile int clientsStartedCount = 0;
		public static volatile int clientsConnectedCount = 0;
		public static volatile int clientsStreamsCount = 0;
		public static volatile int clientsDisconnectedCount = 0;
		public static volatile int clientsReliableSent = 0;
		public static volatile int clientsReliableReceived = 0;
		public static volatile int clientsReliableBytesSent = 0;
		public static volatile int clientsReliableBytesReceived = 0;
		public static volatile int clientsUnreliableSent = 0;
		public static volatile int clientsUnreliableReceived = 0;
		public static volatile int clientsUnreliableBytesSent = 0;
		public static volatile int clientsUnreliableBytesReceived = 0;
		// Libraries
		public static readonly string[] networkingLibraries = {
			"ENet",
			"UNet",
			"LiteNetLib",
			"Lidgren",
			"MiniUDP",
			"Hazel",
			"Photon",
			"Neutrino",
			"DarkRift"
		};
		// Data
		protected static char[] reversedMessage;
		protected static byte[] messageData;
		protected static byte[] reversedData;
		// Internals
		private static bool serverInstance = false;
		private static bool clientsInstance = false;
		private static bool maxClientsPass = true;
		private static bool sustainedLowLatency = false;
		private static ushort maxPeers = 0;
		private static int memoryLength = 512;
		private static BinaryFormatter binaryFormatter;
		private static Thread serverThread;
		private static ServerMessage serverMessage;
		private static ClientsMessage clientsMessage;
		private static MemoryMappedFile serverData;
		private static MemoryMappedFile clientsData;
		private static MemoryMappedViewStream serverStream;
		private static MemoryMappedViewStream clientsStream;
		private static NamedPipeServerStream masterPipeServer;
		private static NamedPipeServerStream masterPipeClients;
		private static Process serverProcess;
		private static Process clientsProcess;
		private	const ushort defaultPort = 9500;
		private	const ushort defaultMaxClients = 1000;
		private const int defaultServerTickRate = 64;
		private	const int defaultClientTickRate = 64;
		private	const int defaultSendRate = 15;
		private	const int defaultReliableMessages = 500;
		private	const int defaultUnreliableMessages = 1000;
		private	const string defaultMessage = "Sometimes we just need a good networking library";
		// Functions
		#if !GUI
			private static readonly Func<int, string> Space = (value) => (String.Empty.PadRight(value));
			private static readonly Func<int, decimal, decimal, decimal> PayloadThroughput = (clientsStreamsCount, messageLength, sendRate) => (clientsStreamsCount * (messageLength * sendRate * 2) * 8 / (1000 * 1000)) * 2;
		#else
			
		#endif

		[Serializable]
		private struct ServerMessage {
			public bool uninitialized;
			public int reliableSent;
			public int reliableReceived;
			public int reliableBytesSent;
			public int reliableBytesReceived;
			public int unreliableSent;
			public int unreliableReceived;
			public int unreliableBytesSent;
			public int unreliableBytesReceived;
		}

		[Serializable]
		private struct ClientsMessage {
			public int StartedCount;
			public int ConnectedCount;
			public int StreamsCount;
			public int DisconnectedCount;
			public int ReliableSent;
			public int ReliableReceived;
			public int ReliableBytesSent;
			public int ReliableBytesReceived;
			public int UnreliableSent;
			public int UnreliableReceived;
			public int UnreliableBytesSent;
			public int UnreliableBytesReceived;
		}

		public static bool Initialize() {
			binaryFormatter = new BinaryFormatter();

			serverData = MemoryMappedFile.CreateOrOpen(title + "ServerData", memoryLength, MemoryMappedFileAccess.ReadWrite);
			serverStream = serverData.CreateViewStream(0, memoryLength);
			binaryFormatter.Serialize(serverStream, serverMessage);
			serverStream.Seek(0, SeekOrigin.Begin);

			clientsData = MemoryMappedFile.CreateOrOpen(title + "ClientsData", memoryLength, MemoryMappedFileAccess.ReadWrite);
			clientsStream = clientsData.CreateViewStream(0, memoryLength);
			binaryFormatter.Serialize(clientsStream, clientsMessage);
			clientsStream.Seek(0, SeekOrigin.Begin);

			if (serverInstance) {
				if (selectedLibrary == 0)
					serverThread = new Thread(ENetBenchmark.Server);
				else if (selectedLibrary == 1)
					serverThread = new Thread(UNetBenchmark.Server);
				else if (selectedLibrary == 2)
					serverThread = new Thread(LiteNetLibBenchmark.Server);
				else if (selectedLibrary == 3)
					serverThread = new Thread(LidgrenBenchmark.Server);
				else if (selectedLibrary == 4)
					serverThread = new Thread(MiniUDPBenchmark.Server);
				else if (selectedLibrary == 5)
					serverThread = new Thread(HazelBenchmark.Server);
				else if (selectedLibrary == 6)
					serverThread = new Thread(PhotonBenchmark.Server);
				else if (selectedLibrary == 7)
					serverThread = new Thread(NeutrinoBenchmark.Server);
				else if (selectedLibrary == 8)
					serverThread = new Thread(DarkRiftBenchmark.Server);

				if (serverThread == null)
					return false;
			}

			UInt16.TryParse(ConfigurationManager.AppSettings["Port"], out port);
			Int32.TryParse(ConfigurationManager.AppSettings["ServerTickRate"], out serverTickRate);
			Int32.TryParse(ConfigurationManager.AppSettings["ClientTickRate"], out clientTickRate);
			Int32.TryParse(ConfigurationManager.AppSettings["SendRate"], out sendRate);
			Int32.TryParse(ConfigurationManager.AppSettings["ReliableMessages"], out reliableMessages);
			Int32.TryParse(ConfigurationManager.AppSettings["UnreliableMessages"], out unreliableMessages);
			message = ConfigurationManager.AppSettings["Message"];
			Boolean.TryParse(ConfigurationManager.AppSettings["SustainedLowLatency"], out sustainedLowLatency);

			if (port == 0)
				port = defaultPort;

			if (maxClients == 0)
				maxClients = defaultMaxClients;

			if (serverTickRate == 0)
				serverTickRate = defaultServerTickRate;

			if (clientTickRate == 0)
				clientTickRate = defaultClientTickRate;

			if (sendRate == 0)
				sendRate = defaultSendRate;

			if (reliableMessages == 0)
				reliableMessages = defaultReliableMessages;

			if (unreliableMessages == 0)
				unreliableMessages = defaultUnreliableMessages;

			if (message.Length == 0)
				message = defaultMessage;

			reversedMessage = message.ToCharArray();
			Array.Reverse(reversedMessage);
			messageData = Encoding.ASCII.GetBytes(message);
			reversedData = Encoding.ASCII.GetBytes(new string(reversedMessage));

			#if !GUI
				Console.CursorVisible = false;
				Console.Clear();
			#endif

			processActive = true;

			if (serverInstance || clientsInstance) {
				if (selectedLibrary == 0)
					ENet.Library.Initialize();
			}

			if (serverInstance) {
				if (sustainedLowLatency)
					GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

				maxPeers = ushort.MaxValue - 1;
				maxClientsPass = (selectedLibrary > 0 ? maxClients <= maxPeers : maxClients <= ENet.Library.maxPeers);

				if (!maxClientsPass)
					maxClients = Math.Min(Math.Max((ushort)1, (ushort)maxClients), (selectedLibrary > 0 ? maxPeers : (ushort)ENet.Library.maxPeers));

				serverThread.Priority = ThreadPriority.AboveNormal;
				serverThread.Start();
				Thread.Sleep(100);
			}

			if (!serverInstance && !clientsInstance) {
				serverProcess = Process.Start(new ProcessStartInfo {
					FileName = Assembly.GetExecutingAssembly().Location,
					Arguments = "-library:" + selectedLibrary + " -server:" + maxClients,
					CreateNoWindow = true,
					UseShellExecute = false
				});

				clientsProcess = Process.Start(new ProcessStartInfo {
					FileName = Assembly.GetExecutingAssembly().Location,
					Arguments = "-library:" + selectedLibrary + " -clients:" + maxClients,
					CreateNoWindow = true,
					UseShellExecute = false
				});
			}

			Task pulseTask = Pulse();
			Task dataTask = Data();

			#if !GUI
				Task infoTask = serverInstance || clientsInstance ? null : Info();
			#endif

			Task superviseTask = serverInstance || clientsInstance ? null : Supervise();
			Task spawnTask = serverInstance || !clientsInstance ? null : Spawn();

			if (serverInstance)
				processUninitialized = false;

			return true;
		}

		private static void Deinitialize() {
			serverProcess.Kill();
			clientsProcess.Kill();
		}

		[STAThread]
		private static void Main(string[] arguments) {
			for (int i = 0; i < arguments.Length; i++) {
				string argument = arguments[i].ToLower();

				if (argument.Contains("-library"))
					Byte.TryParse(argument.Substring(argument.LastIndexOf(":") + 1), out selectedLibrary);

				if (argument.Contains("-server")) {
					serverInstance = true;
					UInt16.TryParse(argument.Substring(argument.LastIndexOf(":") + 1), out maxClients);
				}

				if (argument.Contains("-clients")) {
					clientsInstance = true;
					UInt16.TryParse(argument.Substring(argument.LastIndexOf(":") + 1), out maxClients);
				}
			}

			#if GUI
				
			#else
				Console.Title = title;
				Console.SetIn(new StreamReader(Console.OpenStandardInput(8192), Console.InputEncoding, false, bufferSize: 1024));

				Start:

				if (!serverInstance && !clientsInstance) {
					Console.WriteLine("Welcome to " + title + Space(1) + version + "!");

					Console.WriteLine(Environment.NewLine + "Source code is available on GitHub (https://github.com/nxrighthere/BenchmarkNet)");
					Console.WriteLine("If you have any questions, contact me (nxrighthere@gmail.com)");

					if (sustainedLowLatency) {
						Console.ForegroundColor = ConsoleColor.Yellow;
						Console.WriteLine(Environment.NewLine + "The server process will perform in Sustained Low Latency mode.");
						Console.ResetColor();
					}

					Console.WriteLine(Environment.NewLine + "Select the networking library:");

					for (int i = 0; i < networkingLibraries.Length; i++) {
						Console.WriteLine("(" + i + ") " + networkingLibraries[i]);
					}

					Console.Write(Environment.NewLine + "Enter the number (default 0): ");
					Byte.TryParse(Console.ReadLine(), out selectedLibrary);

					Console.Write("Simulated clients (default " + defaultMaxClients + "): ");
					UInt16.TryParse(Console.ReadLine(), out maxClients);

					if (selectedLibrary >= networkingLibraries.Length) {
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine("Please, enter a valid number of the networking library!");
						Console.ReadKey();
						Console.ResetColor();
						Console.Clear();

						goto Start;
					}
				}

				if (!Initialize()) {
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine("Initialization failed!");
					Console.ReadKey();
					processActive = false;
					Environment.Exit(0);
				}

				Console.ReadKey();
			#endif

			processActive = false;
			
			Deinitialize();

			Environment.Exit(0);
		}

		private static async Task Pulse() {
			await Task.Factory.StartNew(() => {
				const string serverPipeName = title + "Server";
				const string clientsPipeName = title + "Clients";

				if (serverInstance) {
					NamedPipeClientStream serverPipe = new NamedPipeClientStream(".", serverPipeName, PipeDirection.In);

					serverPipe.Connect();
					serverPipe.BeginRead(new byte[1], 0, 1, (result) => { Environment.Exit(0); }, serverPipe);
				} else if (clientsInstance) {
					NamedPipeClientStream clientsPipe = new NamedPipeClientStream(".", clientsPipeName, PipeDirection.In);

					clientsPipe.Connect();
					clientsPipe.BeginRead(new byte[1], 0, 1, (result) => { Environment.Exit(0); }, clientsPipe);
				} else {
					Task.Run(async() => {
						masterPipeServer = new NamedPipeServerStream(serverPipeName, PipeDirection.Out);

						await masterPipeServer.WaitForConnectionAsync();
					});

					Task.Run(async() => {
						masterPipeClients = new NamedPipeServerStream(clientsPipeName, PipeDirection.Out);

						await masterPipeClients.WaitForConnectionAsync();
					});
				}
			}, TaskCreationOptions.LongRunning);
		}

		private static async Task Data() {
			await Task.Factory.StartNew(() => {
				byte[] serverBuffer = serverInstance || clientsInstance ? null : new byte[memoryLength];
				byte[] clientsBuffer = serverInstance || clientsInstance ? null : new byte[memoryLength];

				serverMessage = default(ServerMessage);
				clientsMessage = default(ClientsMessage);

				while (processActive) {
					if (serverInstance) {
						serverMessage.uninitialized = processUninitialized;
						serverMessage.reliableSent = serverReliableSent;
						serverMessage.reliableReceived = serverReliableReceived;
						serverMessage.reliableBytesSent = serverReliableBytesSent;
						serverMessage.reliableBytesReceived = serverReliableBytesReceived;
						serverMessage.unreliableSent = serverUnreliableSent;
						serverMessage.unreliableReceived = serverUnreliableReceived;
						serverMessage.unreliableBytesSent = serverUnreliableBytesSent;
						serverMessage.unreliableBytesReceived = serverUnreliableBytesReceived;

						binaryFormatter.Serialize(serverStream, serverMessage);
						serverStream.Seek(0, SeekOrigin.Begin);
					} else if (clientsInstance) {
						clientsMessage.StartedCount = clientsStartedCount;
						clientsMessage.ConnectedCount = clientsConnectedCount;
						clientsMessage.StreamsCount = clientsStreamsCount;
						clientsMessage.DisconnectedCount = clientsDisconnectedCount;
						clientsMessage.ReliableSent = clientsReliableSent;
						clientsMessage.ReliableReceived = clientsReliableReceived;
						clientsMessage.ReliableBytesSent = clientsReliableBytesSent;
						clientsMessage.ReliableBytesReceived = clientsReliableBytesReceived;
						clientsMessage.UnreliableSent = clientsUnreliableSent;
						clientsMessage.UnreliableReceived = clientsUnreliableReceived;
						clientsMessage.UnreliableBytesSent = clientsUnreliableBytesSent;
						clientsMessage.UnreliableBytesReceived = clientsUnreliableBytesReceived;

						binaryFormatter.Serialize(clientsStream, clientsMessage);
						clientsStream.Seek(0, SeekOrigin.Begin);
					} else {
						serverStream.Read(serverBuffer, 0, memoryLength);
						clientsStream.Read(clientsBuffer, 0, memoryLength);

						using (MemoryStream stream = new MemoryStream(serverBuffer)) {
							serverMessage = (ServerMessage)binaryFormatter.Deserialize(stream);

							processUninitialized = serverMessage.uninitialized;
							serverReliableSent = serverMessage.reliableSent;
							serverReliableReceived = serverMessage.reliableReceived;
							serverReliableBytesSent = serverMessage.reliableBytesSent;
							serverReliableBytesReceived = serverMessage.reliableBytesReceived;
							serverUnreliableSent = serverMessage.unreliableSent;
							serverUnreliableReceived = serverMessage.unreliableReceived;
							serverUnreliableBytesSent = serverMessage.unreliableBytesSent;
							serverUnreliableBytesReceived = serverMessage.unreliableBytesReceived;
						}

						using (MemoryStream stream = new MemoryStream(clientsBuffer)) {
							clientsMessage = (ClientsMessage)binaryFormatter.Deserialize(stream);

							clientsStartedCount = clientsMessage.StartedCount;
							clientsConnectedCount = clientsMessage.ConnectedCount;
							clientsStreamsCount = clientsMessage.StreamsCount;
							clientsDisconnectedCount = clientsMessage.DisconnectedCount;
							clientsReliableSent = clientsMessage.ReliableSent;
							clientsReliableReceived = clientsMessage.ReliableReceived;
							clientsReliableBytesSent = clientsMessage.ReliableBytesSent;
							clientsReliableBytesReceived = clientsMessage.ReliableBytesReceived;
							clientsUnreliableSent = clientsMessage.UnreliableSent;
							clientsUnreliableReceived = clientsMessage.UnreliableReceived;
							clientsUnreliableBytesSent = clientsMessage.UnreliableBytesSent;
							clientsUnreliableBytesReceived = clientsMessage.UnreliableBytesReceived;
						}

						serverStream.Seek(0, SeekOrigin.Begin);
						clientsStream.Seek(0, SeekOrigin.Begin);
					}

					Thread.Sleep(15);
				}
			}, TaskCreationOptions.LongRunning);
		}

		#if !GUI
			private static async Task Info() {
				await Task.Factory.StartNew(() => {
					int spinnerTimer = 0;
					int spinnerSequence = 0;
					string space = Space(10);
					string[] spinner = {
						"/",
						"—",
						"\\",
						"|"
					};
					string[] status = {
						"Running" + Space(6),
						"Crashed" + Space(6),
						"Failure" + Space(6),
						"Overload" + Space(5),
						"Completed" + Space(4),
						"Uninitialized"
					};
					string[] strings = {
						"Benchmarking " + networkingLibraries[selectedLibrary] + "...",
						"Server tick rate: " + serverTickRate + ", Client tick rate: " + clientTickRate + " (ticks per second)",
						maxClients + " clients, " + reliableMessages + " reliable and " + unreliableMessages + " unreliable messages per client, " + sendRate + " messages per second, " + messageData.Length + " bytes per message",
						"GC mode: " + (!GCSettings.IsServerGC ? "Workstation" : "Server"),
						"This networking library doesn't support more than " + (selectedLibrary > 0 ? maxPeers : ENet.Library.maxPeers).ToString() + " peers per server!",
						"The server process is performing in Sustained Low Latency mode.",
					};

					for (int i = 0; i < spinner.Length; i++) {
						spinner[i] = Environment.NewLine + "Press any key to stop the process" + Space(1) + spinner[i];
					}

					Console.WriteLine(strings[0]);
					Console.WriteLine(strings[1]);
					Console.WriteLine(strings[2]);
					Console.WriteLine(strings[3]);

					StringBuilder info = new StringBuilder(1024);
					Stopwatch elapsedTime = Stopwatch.StartNew();

					while (processActive) {
						Console.CursorVisible = false;
						Console.SetCursorPosition(0, 4);

						if (!maxClientsPass || sustainedLowLatency)
							Console.WriteLine();

						if (!maxClientsPass) {
							Console.ForegroundColor = ConsoleColor.Red;
							Console.WriteLine(strings[4]);
							Console.ResetColor();
						}

						if (sustainedLowLatency) {
							Console.ForegroundColor = ConsoleColor.Yellow;
							Console.WriteLine(strings[5]);
							Console.ResetColor();
						}

						info.Clear();
						info.AppendLine().Append("[Server]");
						info.AppendLine().Append("Status: ").Append(processCrashed ? status[1] : (processFailure ? status[2] : (processOverload ? status[3] : (processCompleted ? status[4] : (processUninitialized ? status[5] : status[0])))));
						info.AppendLine().Append("Sent -> Reliable: ").Append(serverReliableSent).Append(" messages (").Append(serverReliableBytesSent).Append(" bytes), Unreliable: ").Append(serverUnreliableSent).Append(" messages (").Append(serverUnreliableBytesSent).Append(" bytes)");
						info.AppendLine().Append("Received <- Reliable: ").Append(serverReliableReceived).Append(" messages (").Append(serverReliableBytesReceived).Append(" bytes), Unreliable: ").Append(serverUnreliableReceived).Append(" messages (").Append(serverUnreliableBytesReceived).Append(" bytes)");
						info.AppendLine().Append("Payload throughput: ").Append(PayloadThroughput(clientsStreamsCount, messageData.Length, sendRate).ToString("0.00")).Append(" mbps \\ ").Append(PayloadThroughput(maxClients * 2, messageData.Length, sendRate).ToString("0.00")).Append(" mbps").Append(space);
						info.AppendLine();
						info.AppendLine().Append("[Clients]");
						info.AppendLine().Append("Status: ").Append(clientsStartedCount).Append(" started, ").Append(clientsConnectedCount).Append(" connected, ").Append(clientsDisconnectedCount).Append(" dropped");
						info.AppendLine().Append("Sent -> Reliable: ").Append(clientsReliableSent).Append(" messages (").Append(clientsReliableBytesSent).Append(" bytes), Unreliable: ").Append(clientsUnreliableSent).Append(" messages (").Append(clientsUnreliableBytesSent).Append(" bytes)");
						info.AppendLine().Append("Received <- Reliable: ").Append(clientsReliableReceived).Append(" messages (").Append(clientsReliableBytesReceived).Append(" bytes), Unreliable: ").Append(clientsUnreliableReceived).Append(" messages (").Append(clientsUnreliableBytesReceived).Append(" bytes)");
						info.AppendLine();
						info.AppendLine().Append("[Summary]");
						info.AppendLine().Append("Total - Reliable: ").Append((ulong)clientsReliableSent + (ulong)serverReliableReceived + (ulong)serverReliableSent + (ulong)clientsReliableReceived).Append(" messages (").Append((ulong)clientsReliableBytesSent + (ulong)serverReliableBytesReceived + (ulong)serverReliableBytesSent + (ulong)clientsReliableBytesReceived).Append(" bytes), Unreliable: ").Append((ulong)clientsUnreliableSent + (ulong)serverUnreliableReceived + (ulong)serverUnreliableSent + (ulong)clientsUnreliableReceived).Append(" messages (").Append((ulong)clientsUnreliableBytesSent + (ulong)serverUnreliableBytesReceived + (ulong)serverUnreliableBytesSent + (ulong)clientsUnreliableBytesReceived).Append(" bytes)");
						info.AppendLine().Append("Expected - Reliable: ").Append(maxClients * (ulong)reliableMessages * 4).Append(" messages (").Append(maxClients * (ulong)reliableMessages * (ulong)messageData.Length * 4).Append(" bytes), Unreliable: ").Append(maxClients * (ulong)unreliableMessages * 4).Append(" messages (").Append(maxClients * (ulong)unreliableMessages * (ulong)messageData.Length * 4).Append(" bytes)");
						info.AppendLine().Append("Elapsed time: ").Append(elapsedTime.Elapsed.Hours.ToString("00")).Append(":").Append(elapsedTime.Elapsed.Minutes.ToString("00")).Append(":").Append(elapsedTime.Elapsed.Seconds.ToString("00"));
						Console.WriteLine(info);

						if (spinnerTimer >= 10) {
							spinnerSequence++;
							spinnerTimer = 0;

							if (spinnerSequence == spinner.Length)
								spinnerSequence = 0;
						} else {
							spinnerTimer++;
						}

						Console.WriteLine(spinner[spinnerSequence]);
						Thread.Sleep(1000 / 60);
					}

					elapsedTime.Stop();

					if (!processActive && processCompleted) {
						Console.SetCursorPosition(0, Console.CursorTop - 1);
						Console.WriteLine("Process completed! Press any key to exit...");
					}
				}, TaskCreationOptions.LongRunning);
			}
		#endif

		private static async Task Supervise() {
			await Task.Factory.StartNew(() => {
				decimal currentData = 0;
				decimal lastData = 0;
				bool recollectData = true;

				while (processActive) {
					Thread.Sleep(1000);

					Collect:
					currentData = ((decimal)serverReliableSent + (decimal)serverReliableReceived + (decimal)serverUnreliableSent + (decimal)serverUnreliableReceived + (decimal)clientsReliableSent + (decimal)clientsReliableReceived + (decimal)clientsUnreliableSent + (decimal)clientsUnreliableReceived);

					if (serverProcess.HasExited)
						processCrashed = true;

					if (currentData == lastData) {
						if (currentData == 0) {
							if (recollectData) {
								recollectData = false;
								Thread.Sleep(4000);

								goto Collect;
							}

							processFailure = true;
						} else if (clientsDisconnectedCount > 1 || ((currentData / (maxClients * ((decimal)reliableMessages + (decimal)unreliableMessages) * 4)) * 100) < 90) {
							processOverload = true;
						}

						processCompleted = true;
						Thread.Sleep(100);
						processActive = false;

						Deinitialize();						

						break;
					}

					lastData = currentData;
				}

				if (serverInstance || clientsInstance) {
					if (selectedLibrary == 0)
						ENet.Library.Deinitialize();
				}
			}, TaskCreationOptions.LongRunning);
		}

		private static async Task Spawn() {
			await Task.Factory.StartNew(() => {
				Task[] clients = new Task[maxClients];

				for (int i = 0; i < maxClients; i++) {
					if (!processActive)
						break;

					if (selectedLibrary == 0)
						clients[i] = ENetBenchmark.Client();
					else if (selectedLibrary == 1)
						clients[i] = UNetBenchmark.Client();
					else if (selectedLibrary == 2)
						clients[i] = LiteNetLibBenchmark.Client();
					else if (selectedLibrary == 3)
						clients[i] = LidgrenBenchmark.Client();
					else if (selectedLibrary == 4)
						clients[i] = MiniUDPBenchmark.Client();
					else if (selectedLibrary == 5)
						clients[i] = HazelBenchmark.Client();
					else if (selectedLibrary == 6)
						clients[i] = PhotonBenchmark.Client();
					else if (selectedLibrary == 7)
						clients[i] = NeutrinoBenchmark.Client();
					else if (selectedLibrary == 8)
						clients[i] = DarkRiftBenchmark.Client();

					Interlocked.Increment(ref clientsStartedCount);
					Thread.Sleep(15);
				}
			}, TaskCreationOptions.LongRunning);
		}
	}

	public sealed class ENetBenchmark : BenchmarkNet {
		private static void SendReliable(byte[] data, byte channelID, Peer peer) {
			Packet packet = default(Packet);

			packet.Create(data, data.Length, PacketFlags.Reliable | PacketFlags.NoAllocate); // Reliable Sequenced
			peer.Send(channelID, ref packet);
		}

		private static void SendUnreliable(byte[] data, byte channelID, Peer peer) {
			Packet packet = default(Packet);

			packet.Create(data, data.Length, PacketFlags.None | PacketFlags.NoAllocate); // Unreliable Sequenced
			peer.Send(channelID, ref packet);
		}

		public static void Server() {
			using (Host server = new Host()) {
				Address address = new Address();

				address.Port = port;

				server.Create(address, maxClients, 4);

				while (processActive) {
					server.Service(1000 / serverTickRate, out Event netEvent);

					switch (netEvent.Type) {
						case EventType.None:
							break;

						case EventType.Receive:
							if (netEvent.ChannelID == 2) {
								Interlocked.Increment(ref serverReliableReceived);
								Interlocked.Add(ref serverReliableBytesReceived, netEvent.Packet.Length);
								SendReliable(messageData, 0, netEvent.Peer);
								Interlocked.Increment(ref serverReliableSent);
								Interlocked.Add(ref serverReliableBytesSent, messageData.Length);
							} else if (netEvent.ChannelID == 3) {
								Interlocked.Increment(ref serverUnreliableReceived);
								Interlocked.Add(ref serverUnreliableBytesReceived, netEvent.Packet.Length);
								SendUnreliable(reversedData, 1, netEvent.Peer);
								Interlocked.Increment(ref serverUnreliableSent);
								Interlocked.Add(ref serverUnreliableBytesSent, reversedData.Length);
							}

							netEvent.Packet.Dispose();

							break;
					}
				}
			}
		}

		public static async Task Client() {
			await Task.Factory.StartNew(() => {
				using (Host client = new Host()) {
					Address address = new Address();

					address.SetHost(ip);
					address.Port = port;

					client.Create();

					Peer peer = client.Connect(address, 4);

					int reliableToSend = 0;
					int unreliableToSend = 0;
					int reliableSentCount = 0;
					int unreliableSentCount = 0;

					Task.Factory.StartNew(async() => {
						bool reliableIncremented = false;
						bool unreliableIncremented = false;

						while (processActive) {
							if (reliableToSend > 0) {
								SendReliable(messageData, 2, peer);
								Interlocked.Decrement(ref reliableToSend);
								Interlocked.Increment(ref reliableSentCount);
								Interlocked.Increment(ref clientsReliableSent);
								Interlocked.Add(ref clientsReliableBytesSent, messageData.Length);
							}

							if (unreliableToSend > 0) {
								SendUnreliable(reversedData, 3, peer);
								Interlocked.Decrement(ref unreliableToSend);
								Interlocked.Increment(ref unreliableSentCount);
								Interlocked.Increment(ref clientsUnreliableSent);
								Interlocked.Add(ref clientsUnreliableBytesSent, reversedData.Length);
							}

							if (reliableToSend > 0 && !reliableIncremented) {
								reliableIncremented = true;
								Interlocked.Increment(ref clientsStreamsCount);
							} else if (reliableToSend == 0 && reliableIncremented) {
								reliableIncremented = false;
								Interlocked.Decrement(ref clientsStreamsCount);
							}

							if (unreliableToSend > 0 && !unreliableIncremented) {
								unreliableIncremented = true;
								Interlocked.Increment(ref clientsStreamsCount);
							} else if (unreliableToSend == 0 && unreliableIncremented) {
								unreliableIncremented = false;
								Interlocked.Decrement(ref clientsStreamsCount);
							}

							await Task.Delay(1000 / sendRate);
						}
					}, TaskCreationOptions.AttachedToParent);

					while (processActive) {
						client.Service(1000 / clientTickRate, out Event netEvent);

						switch (netEvent.Type) {
							case EventType.None:
								break;

							case EventType.Connect:
								Interlocked.Increment(ref clientsConnectedCount);
								Interlocked.Exchange(ref reliableToSend, reliableMessages);
								Interlocked.Exchange(ref unreliableToSend, unreliableMessages);

								break;

							case EventType.Disconnect: case EventType.Timeout:
								Interlocked.Increment(ref clientsDisconnectedCount);
								Interlocked.Exchange(ref reliableToSend, 0);
								Interlocked.Exchange(ref unreliableToSend, 0);

								break;

							case EventType.Receive:
								if (netEvent.ChannelID == 0) {
									Interlocked.Increment(ref clientsReliableReceived);
									Interlocked.Add(ref clientsReliableBytesReceived, netEvent.Packet.Length);
								} else if (netEvent.ChannelID == 1) {
									Interlocked.Increment(ref clientsUnreliableReceived);
									Interlocked.Add(ref clientsUnreliableBytesReceived, netEvent.Packet.Length);
								}

								netEvent.Packet.Dispose();

								break;
						}
					}

					peer.Disconnect(0);
				}
			}, TaskCreationOptions.LongRunning);
		}
	}

	public sealed class UNetBenchmark : BenchmarkNet {
		public static void Server() {
			GlobalConfig globalConfig = new GlobalConfig();

			globalConfig.ThreadPoolSize = 4;
			globalConfig.ThreadAwakeTimeout = (uint)serverTickRate;
			globalConfig.MaxHosts = 1;
			globalConfig.MaxPacketSize = (ushort)((messageData.Length * 2) + 32);
			globalConfig.ReactorMaximumSentMessages = (ushort)((reliableMessages + unreliableMessages) * sendRate);
			globalConfig.ReactorMaximumReceivedMessages = (ushort)((reliableMessages + unreliableMessages) * sendRate);

			ConnectionConfig connectionConfig = new ConnectionConfig();

			int reliableChannel = connectionConfig.AddChannel(QosType.ReliableSequenced);
			int unreliableChannel = connectionConfig.AddChannel(QosType.UnreliableSequenced);

			connectionConfig.SendDelay = 1;
			connectionConfig.MinUpdateTimeout = 1;
			connectionConfig.PingTimeout = 2000;
			connectionConfig.DisconnectTimeout = 5000;
			connectionConfig.PacketSize = (ushort)((messageData.Length * 2) + 32);

			HostTopology topology = new HostTopology(connectionConfig, maxClients);

			topology.SentMessagePoolSize = (ushort)((reliableMessages + unreliableMessages) * sendRate);
			topology.ReceivedMessagePoolSize = (ushort)((reliableMessages + unreliableMessages) * sendRate);

			NetLibraryManager server = new NetLibraryManager(globalConfig);

			int host = server.AddHost(topology, port, ip);
			byte[] buffer = new byte[1024];
			NetworkEventType netEvent;

			while (processActive) {
				while ((netEvent = server.Receive(out int hostID, out int connectionID, out int channelID, buffer, buffer.Length, out int dataLength, out byte netError)) != NetworkEventType.Nothing) {
					switch (netEvent) {
						case NetworkEventType.DataEvent:
							if (channelID == 0) {
								Interlocked.Increment(ref serverReliableReceived);
								Interlocked.Add(ref serverReliableBytesReceived, dataLength);
								server.Send(hostID, connectionID, reliableChannel, messageData, messageData.Length, out byte sendError);
								Interlocked.Increment(ref serverReliableSent);
								Interlocked.Add(ref serverReliableBytesSent, messageData.Length);
							} else if (channelID == 1) {
								Interlocked.Increment(ref serverUnreliableReceived);
								Interlocked.Add(ref serverUnreliableBytesReceived, dataLength);
								server.Send(hostID, connectionID, unreliableChannel, reversedData, reversedData.Length, out byte sendError);
								Interlocked.Increment(ref serverUnreliableSent);
								Interlocked.Add(ref serverUnreliableBytesSent, reversedData.Length);
							}

							break;
					}
				}

				Thread.Sleep(1000 / serverTickRate);
			}
		}

		public static async Task Client() {
			await Task.Factory.StartNew(() => {
				GlobalConfig globalConfig = new GlobalConfig();

				globalConfig.ThreadPoolSize = 1;
				globalConfig.ThreadAwakeTimeout = (uint)clientTickRate;
				globalConfig.MaxHosts = 1;
				globalConfig.MaxPacketSize = (ushort)((messageData.Length * 2) + 32);
				globalConfig.ReactorMaximumSentMessages = (ushort)(sendRate / 2);
				globalConfig.ReactorMaximumReceivedMessages = (ushort)(sendRate / 2);

				ConnectionConfig connectionConfig = new ConnectionConfig();

				int reliableChannel = connectionConfig.AddChannel(QosType.ReliableSequenced);
				int unreliableChannel = connectionConfig.AddChannel(QosType.UnreliableSequenced);

				connectionConfig.SendDelay = 1;
				connectionConfig.MinUpdateTimeout = 1;
				connectionConfig.PingTimeout = 2000;
				connectionConfig.DisconnectTimeout = 5000;
				connectionConfig.PacketSize = (ushort)((messageData.Length * 2) + 32);

				HostTopology topology = new HostTopology(connectionConfig, 1);

				topology.SentMessagePoolSize = (ushort)(sendRate / 2);
				topology.ReceivedMessagePoolSize = (ushort)(sendRate / 2);

				NetLibraryManager client = new NetLibraryManager(globalConfig);

				int host = client.AddHost(topology, 0, null);
				int connection = client.Connect(host, ip, port, 0, out byte connectionError);

				int reliableToSend = 0;
				int unreliableToSend = 0;
				int reliableSentCount = 0;
				int unreliableSentCount = 0;

				Task.Factory.StartNew(async() => {
					bool reliableIncremented = false;
					bool unreliableIncremented = false;

					while (processActive) {
						if (reliableToSend > 0) {
							client.Send(host, connection, reliableChannel, messageData, messageData.Length, out byte sendError);
							Interlocked.Decrement(ref reliableToSend);
							Interlocked.Increment(ref reliableSentCount);
							Interlocked.Increment(ref clientsReliableSent);
							Interlocked.Add(ref clientsReliableBytesSent, messageData.Length);
						}

						if (unreliableToSend > 0) {
							client.Send(host, connection, unreliableChannel, reversedData, reversedData.Length, out byte sendError);
							Interlocked.Decrement(ref unreliableToSend);
							Interlocked.Increment(ref unreliableSentCount);
							Interlocked.Increment(ref clientsUnreliableSent);
							Interlocked.Add(ref clientsUnreliableBytesSent, reversedData.Length);
						}

						if (reliableToSend > 0 && !reliableIncremented) {
							reliableIncremented = true;
							Interlocked.Increment(ref clientsStreamsCount);
						} else if (reliableToSend == 0 && reliableIncremented) {
							reliableIncremented = false;
							Interlocked.Decrement(ref clientsStreamsCount);
						}

						if (unreliableToSend > 0 && !unreliableIncremented) {
							unreliableIncremented = true;
							Interlocked.Increment(ref clientsStreamsCount);
						} else if (unreliableToSend == 0 && unreliableIncremented) {
							unreliableIncremented = false;
							Interlocked.Decrement(ref clientsStreamsCount);
						}

						await Task.Delay(1000 / sendRate);
					}
				}, TaskCreationOptions.AttachedToParent);

				byte[] buffer = new byte[1024];
				NetworkEventType netEvent;

				while (processActive) {
					while ((netEvent = client.Receive(out int hostID, out int connectionID, out int channelID, buffer, buffer.Length, out int dataLength, out byte netError)) != NetworkEventType.Nothing) {
						switch (netEvent) {
							case NetworkEventType.ConnectEvent:
								Interlocked.Increment(ref clientsConnectedCount);
								Interlocked.Exchange(ref reliableToSend, reliableMessages);
								Interlocked.Exchange(ref unreliableToSend, unreliableMessages);

								break;

							case NetworkEventType.DisconnectEvent:
								Interlocked.Increment(ref clientsDisconnectedCount);
								Interlocked.Exchange(ref reliableToSend, 0);
								Interlocked.Exchange(ref unreliableToSend, 0);

								break;

							case NetworkEventType.DataEvent:
								if (channelID == 0) {
									Interlocked.Increment(ref clientsReliableReceived);
									Interlocked.Add(ref clientsReliableBytesReceived, dataLength);
								} else if (channelID == 1) {
									Interlocked.Increment(ref clientsUnreliableReceived);
									Interlocked.Add(ref clientsUnreliableBytesReceived, dataLength);
								}

								break;
						}
					}

					Thread.Sleep(1000 / clientTickRate);
				}

				client.Disconnect(host, connection, out byte disconnectError);
			}, TaskCreationOptions.LongRunning);
		}
	}

	public sealed class LiteNetLibBenchmark : BenchmarkNet {
		public static void Server() {
			EventBasedNetListener listener = new EventBasedNetListener();
			NetManager server = new NetManager(listener);

			server.MergeEnabled = true;
			server.Start(port);

			listener.ConnectionRequestEvent += (request) => {
				request.AcceptIfKey(title + "Key");
			};

			listener.NetworkReceiveEvent += (peer, reader, deliveryMethod) => {
				if (deliveryMethod == DeliveryMethod.ReliableOrdered) {
					Interlocked.Increment(ref serverReliableReceived);
					Interlocked.Add(ref serverReliableBytesReceived, reader.AvailableBytes);
					peer.Send(messageData, DeliveryMethod.ReliableOrdered);
					Interlocked.Increment(ref serverReliableSent);
					Interlocked.Add(ref serverReliableBytesSent, messageData.Length);
				} else if (deliveryMethod == DeliveryMethod.Sequenced) {
					Interlocked.Increment(ref serverUnreliableReceived);
					Interlocked.Add(ref serverUnreliableBytesReceived, reader.AvailableBytes);
					peer.Send(reversedData, DeliveryMethod.Sequenced);
					Interlocked.Increment(ref serverUnreliableSent);
					Interlocked.Add(ref serverUnreliableBytesSent, reversedData.Length);
				}

				reader.Recycle();
			};

			while (processActive) {
				server.PollEvents();
				Thread.Sleep(1000 / serverTickRate);
			}

			server.Stop();
		}

		public static async Task Client() {
			await Task.Factory.StartNew(() => {
				EventBasedNetListener listener = new EventBasedNetListener();
				NetManager client = new NetManager(listener);

				client.MergeEnabled = true;
				client.Start();
				client.Connect(ip, port, title + "Key");

				LiteNetLib.NetPeer connection = client.GetFirstPeer();

				int reliableToSend = 0;
				int unreliableToSend = 0;
				int reliableSentCount = 0;
				int unreliableSentCount = 0;

				Task.Factory.StartNew(async() => {
					bool reliableIncremented = false;
					bool unreliableIncremented = false;

					while (processActive) {
						if (reliableToSend > 0) {
							connection.Send(messageData, DeliveryMethod.ReliableOrdered);
							Interlocked.Decrement(ref reliableToSend);
							Interlocked.Increment(ref reliableSentCount);
							Interlocked.Increment(ref clientsReliableSent);
							Interlocked.Add(ref clientsReliableBytesSent, messageData.Length);
						}

						if (unreliableToSend > 0) {
							connection.Send(reversedData, DeliveryMethod.Sequenced);
							Interlocked.Decrement(ref unreliableToSend);
							Interlocked.Increment(ref unreliableSentCount);
							Interlocked.Increment(ref clientsUnreliableSent);
							Interlocked.Add(ref clientsUnreliableBytesSent, reversedData.Length);
						}

						if (reliableToSend > 0 && !reliableIncremented) {
							reliableIncremented = true;
							Interlocked.Increment(ref clientsStreamsCount);
						} else if (reliableToSend == 0 && reliableIncremented) {
							reliableIncremented = false;
							Interlocked.Decrement(ref clientsStreamsCount);
						}

						if (unreliableToSend > 0 && !unreliableIncremented) {
							unreliableIncremented = true;
							Interlocked.Increment(ref clientsStreamsCount);
						} else if (unreliableToSend == 0 && unreliableIncremented) {
							unreliableIncremented = false;
							Interlocked.Decrement(ref clientsStreamsCount);
						}

						await Task.Delay(1000 / sendRate);
					}
				}, TaskCreationOptions.AttachedToParent);

				listener.PeerConnectedEvent += (peer) => {
					Interlocked.Increment(ref clientsConnectedCount);
					Interlocked.Exchange(ref reliableToSend, reliableMessages);
					Interlocked.Exchange(ref unreliableToSend, unreliableMessages);
				};

				listener.PeerDisconnectedEvent += (peer, info) => {
					Interlocked.Increment(ref clientsDisconnectedCount);
					Interlocked.Exchange(ref reliableToSend, 0);
					Interlocked.Exchange(ref unreliableToSend, 0);
				};

				listener.NetworkReceiveEvent += (peer, reader, deliveryMethod) => {
					if (deliveryMethod == DeliveryMethod.ReliableOrdered) {
						Interlocked.Increment(ref clientsReliableReceived);
						Interlocked.Add(ref clientsReliableBytesReceived, reader.AvailableBytes);
					} else if (deliveryMethod == DeliveryMethod.Sequenced) {
						Interlocked.Increment(ref clientsUnreliableReceived);
						Interlocked.Add(ref clientsUnreliableBytesReceived, reader.AvailableBytes);
					}

					reader.Recycle();
				};

				while (processActive) {
					client.PollEvents();
					Thread.Sleep(1000 / clientTickRate);
				}

				client.Stop();
			}, TaskCreationOptions.LongRunning);
		}
	}

	public sealed class LidgrenBenchmark : BenchmarkNet {
		private static void SendReliable(byte[] data, NetConnection connection, NetOutgoingMessage netMessage, int channelID) {
			netMessage.Write(data);
			connection.SendMessage(netMessage, NetDeliveryMethod.ReliableSequenced, channelID);
		}

		private static void SendUnreliable(byte[] data, NetConnection connection, NetOutgoingMessage netMessage, int channelID) {
			netMessage.Write(data);
			connection.SendMessage(netMessage, NetDeliveryMethod.UnreliableSequenced, channelID);
		}

		public static void Server() {
			NetPeerConfiguration config = new NetPeerConfiguration(title + "Config");

			config.Port = port;
			config.MaximumConnections = maxClients;

			NetServer server = new NetServer(config);
			server.Start();

			NetIncomingMessage netMessage;

			while (processActive) {
				while ((netMessage = server.ReadMessage()) != null) {
					switch (netMessage.MessageType) {
						case NetIncomingMessageType.Data:
							if (netMessage.SequenceChannel == 2) {
								Interlocked.Increment(ref serverReliableReceived);
								Interlocked.Add(ref serverReliableBytesReceived, netMessage.LengthBytes);
								SendReliable(messageData, netMessage.SenderConnection, server.CreateMessage(), 0);
								Interlocked.Increment(ref serverReliableSent);
								Interlocked.Add(ref serverReliableBytesSent, messageData.Length);
							} else if (netMessage.SequenceChannel == 3) {
								Interlocked.Increment(ref serverUnreliableReceived);
								Interlocked.Add(ref serverUnreliableBytesReceived, netMessage.LengthBytes);
								SendUnreliable(reversedData, netMessage.SenderConnection, server.CreateMessage(), 1);
								Interlocked.Increment(ref serverUnreliableSent);
								Interlocked.Add(ref serverUnreliableBytesSent, reversedData.Length);
							}

							break;
					}

					server.Recycle(netMessage);
				}

				Thread.Sleep(1000 / serverTickRate);
			}

			server.Shutdown(title + "Shutdown");
		}

		public static async Task Client() {
			await Task.Factory.StartNew(() => {
				NetPeerConfiguration config = new NetPeerConfiguration(title + "Config");
				NetClient client = new NetClient(config);

				client.Start();
				client.Connect(ip, port);

				int reliableToSend = 0;
				int unreliableToSend = 0;
				int reliableSentCount = 0;
				int unreliableSentCount = 0;

				Task.Factory.StartNew(async() => {
					bool reliableIncremented = false;
					bool unreliableIncremented = false;

					while (processActive) {
						if (reliableToSend > 0) {
							SendReliable(messageData, client.ServerConnection, client.CreateMessage(), 2);
							Interlocked.Decrement(ref reliableToSend);
							Interlocked.Increment(ref reliableSentCount);
							Interlocked.Increment(ref clientsReliableSent);
							Interlocked.Add(ref clientsReliableBytesSent, messageData.Length);
						}

						if (unreliableToSend > 0) {
							SendUnreliable(reversedData, client.ServerConnection, client.CreateMessage(), 3);
							Interlocked.Decrement(ref unreliableToSend);
							Interlocked.Increment(ref unreliableSentCount);
							Interlocked.Increment(ref clientsUnreliableSent);
							Interlocked.Add(ref clientsUnreliableBytesSent, reversedData.Length);
						}

						if (reliableToSend > 0 && !reliableIncremented) {
							reliableIncremented = true;
							Interlocked.Increment(ref clientsStreamsCount);
						} else if (reliableToSend == 0 && reliableIncremented) {
							reliableIncremented = false;
							Interlocked.Decrement(ref clientsStreamsCount);
						}

						if (unreliableToSend > 0 && !unreliableIncremented) {
							unreliableIncremented = true;
							Interlocked.Increment(ref clientsStreamsCount);
						} else if (unreliableToSend == 0 && unreliableIncremented) {
							unreliableIncremented = false;
							Interlocked.Decrement(ref clientsStreamsCount);
						}

						await Task.Delay(1000 / sendRate);
					}
				}, TaskCreationOptions.AttachedToParent);

				NetIncomingMessage netMessage;

				while (processActive) {
					while ((netMessage = client.ReadMessage()) != null) {
						switch (netMessage.MessageType) {
							case NetIncomingMessageType.StatusChanged:
								NetConnectionStatus status = (NetConnectionStatus)netMessage.ReadByte();

								if (status == NetConnectionStatus.Connected) {
									Interlocked.Increment(ref clientsConnectedCount);
									Interlocked.Exchange(ref reliableToSend, reliableMessages);
									Interlocked.Exchange(ref unreliableToSend, unreliableMessages);
								} else if (status == NetConnectionStatus.Disconnected) {
									Interlocked.Increment(ref clientsDisconnectedCount);
									Interlocked.Exchange(ref reliableToSend, 0);
									Interlocked.Exchange(ref unreliableToSend, 0);
								}

								break;

							case NetIncomingMessageType.Data:
								if (netMessage.SequenceChannel == 0) {
									Interlocked.Increment(ref clientsReliableReceived);
									Interlocked.Add(ref clientsReliableBytesReceived, netMessage.LengthBytes);
								} else if (netMessage.SequenceChannel == 1) {
									Interlocked.Increment(ref clientsUnreliableReceived);
									Interlocked.Add(ref clientsUnreliableBytesReceived, netMessage.LengthBytes);
								}

								break;
						}

						client.Recycle(netMessage);
					}

					Thread.Sleep(1000 / clientTickRate);
				}

				client.Shutdown(title + "Shutdown");
			}, TaskCreationOptions.LongRunning);
		}
	}

	public sealed class MiniUDPBenchmark : BenchmarkNet {
		public static void Server() {
			NetCore server = new NetCore(title, true);

			server.Host(port);

			server.PeerNotification += (peer, data, dataLength) => {
				Interlocked.Increment(ref serverReliableReceived);
				Interlocked.Add(ref serverReliableBytesReceived, dataLength);
				peer.QueueNotification(messageData, (ushort)messageData.Length);
				Interlocked.Increment(ref serverReliableSent);
				Interlocked.Add(ref serverReliableBytesSent, messageData.Length);
			};

			server.PeerPayload += (peer, data, dataLength) => {
				Interlocked.Increment(ref serverUnreliableReceived);
				Interlocked.Add(ref serverUnreliableBytesReceived, dataLength);
				peer.SendPayload(reversedData, (ushort)reversedData.Length);
				Interlocked.Increment(ref serverUnreliableSent);
				Interlocked.Add(ref serverUnreliableBytesSent, reversedData.Length);
			};

			while (processActive) {
				server.PollEvents();
				Thread.Sleep(1000 / serverTickRate);
			}

			server.Stop();
		}

		public static async Task Client() {
			await Task.Factory.StartNew(() => {
				NetCore client = new NetCore(title, false);

				MiniUDP.NetPeer connection = client.Connect(NetUtil.StringToEndPoint(ip + ":" + port), String.Empty);

				int reliableToSend = 0;
				int unreliableToSend = 0;
				int reliableSentCount = 0;
				int unreliableSentCount = 0;

				Task.Factory.StartNew(async() => {
					bool reliableIncremented = false;
					bool unreliableIncremented = false;

					while (processActive) {
						if (reliableToSend > 0) {
							connection.QueueNotification(messageData, (ushort)messageData.Length);
							Interlocked.Decrement(ref reliableToSend);
							Interlocked.Increment(ref reliableSentCount);
							Interlocked.Increment(ref clientsReliableSent);
							Interlocked.Add(ref clientsReliableBytesSent, messageData.Length);
						}

						if (unreliableToSend > 0) {
							connection.SendPayload(reversedData, (ushort)reversedData.Length);
							Interlocked.Decrement(ref unreliableToSend);
							Interlocked.Increment(ref unreliableSentCount);
							Interlocked.Increment(ref clientsUnreliableSent);
							Interlocked.Add(ref clientsUnreliableBytesSent, reversedData.Length);
						}

						if (reliableToSend > 0 && !reliableIncremented) {
							reliableIncremented = true;
							Interlocked.Increment(ref clientsStreamsCount);
						} else if (reliableToSend == 0 && reliableIncremented) {
							reliableIncremented = false;
							Interlocked.Decrement(ref clientsStreamsCount);
						}

						if (unreliableToSend > 0 && !unreliableIncremented) {
							unreliableIncremented = true;
							Interlocked.Increment(ref clientsStreamsCount);
						} else if (unreliableToSend == 0 && unreliableIncremented) {
							unreliableIncremented = false;
							Interlocked.Decrement(ref clientsStreamsCount);
						}

						await Task.Delay(1000 / sendRate);
					}
				}, TaskCreationOptions.AttachedToParent);

				client.PeerConnected += (peer, token) => {
					Interlocked.Increment(ref clientsConnectedCount);
					Interlocked.Exchange(ref reliableToSend, reliableMessages);
					Interlocked.Exchange(ref unreliableToSend, unreliableMessages);
				};

				client.PeerClosed += (peer, reason, kickReason, error) => {
					Interlocked.Increment(ref clientsDisconnectedCount);
					Interlocked.Exchange(ref reliableToSend, 0);
					Interlocked.Exchange(ref unreliableToSend, 0);
				};

				client.PeerNotification += (peer, data, dataLength) => {
					Interlocked.Increment(ref clientsReliableReceived);
					Interlocked.Add(ref clientsReliableBytesReceived, dataLength);
				};

				client.PeerPayload += (peer, data, dataLength) => {
					Interlocked.Increment(ref clientsUnreliableReceived);
					Interlocked.Add(ref clientsUnreliableBytesReceived, dataLength);
				};

				while (processActive) {
					client.PollEvents();
					Thread.Sleep(1000 / clientTickRate);
				}

				client.Stop();
			}, TaskCreationOptions.LongRunning);
		}
	}

	public sealed class HazelBenchmark : BenchmarkNet {
		public static void Server() {
			UdpConnectionListener server = new UdpConnectionListener(new NetworkEndPoint(ip, port));

			server.NewConnection += (peer, netEvent) => {
				netEvent.Connection.DataReceived += (sender, data) => {
					Connection client = (Connection)sender;

					if (data.SendOption == SendOption.Reliable) {
						Interlocked.Increment(ref serverReliableReceived);
						Interlocked.Add(ref serverReliableBytesReceived, data.Bytes.Length);

						if (client.State == Hazel.ConnectionState.Connected) {
							client.SendBytes(messageData, SendOption.Reliable);
							Interlocked.Increment(ref serverReliableSent);
							Interlocked.Add(ref serverReliableBytesSent, messageData.Length);
						}
					} else if (data.SendOption == SendOption.None) {
						Interlocked.Increment(ref serverUnreliableReceived);
						Interlocked.Add(ref serverUnreliableBytesReceived, data.Bytes.Length);

						if (client.State == Hazel.ConnectionState.Connected) {
							client.SendBytes(reversedData, SendOption.None);
							Interlocked.Increment(ref serverUnreliableSent);
							Interlocked.Add(ref serverUnreliableBytesSent, reversedData.Length);
						}
					}

					data.Recycle();
				};

				netEvent.Recycle();
			};

			server.Start();

			while (processActive) {
				Thread.Sleep(1000 / serverTickRate);
			}

			server.Close();
		}

		public static async Task Client() {
			await Task.Factory.StartNew(() => {
				UdpClientConnection client = new UdpClientConnection(new NetworkEndPoint(ip, port));

				client.Connect();

				int reliableToSend = 0;
				int unreliableToSend = 0;
				int reliableSentCount = 0;
				int unreliableSentCount = 0;

				Task.Factory.StartNew(async() => {
					bool reliableIncremented = false;
					bool unreliableIncremented = false;

					while (processActive) {
						if (reliableToSend > 0) {
							client.SendBytes(messageData, SendOption.Reliable);
							Interlocked.Decrement(ref reliableToSend);
							Interlocked.Increment(ref reliableSentCount);
							Interlocked.Increment(ref clientsReliableSent);
							Interlocked.Add(ref clientsReliableBytesSent, messageData.Length);
						}

						if (unreliableToSend > 0) {
							client.SendBytes(reversedData, SendOption.None);
							Interlocked.Decrement(ref unreliableToSend);
							Interlocked.Increment(ref unreliableSentCount);
							Interlocked.Increment(ref clientsUnreliableSent);
							Interlocked.Add(ref clientsUnreliableBytesSent, reversedData.Length);
						}

						if (reliableToSend > 0 && !reliableIncremented) {
							reliableIncremented = true;
							Interlocked.Increment(ref clientsStreamsCount);
						} else if (reliableToSend == 0 && reliableIncremented) {
							reliableIncremented = false;
							Interlocked.Decrement(ref clientsStreamsCount);
						}

						if (unreliableToSend > 0 && !unreliableIncremented) {
							unreliableIncremented = true;
							Interlocked.Increment(ref clientsStreamsCount);
						} else if (unreliableToSend == 0 && unreliableIncremented) {
							unreliableIncremented = false;
							Interlocked.Decrement(ref clientsStreamsCount);
						}

						await Task.Delay(1000 / sendRate);
					}
				}, TaskCreationOptions.AttachedToParent);

				client.Disconnected += (sender, data) => {
					Interlocked.Increment(ref clientsDisconnectedCount);
					Interlocked.Exchange(ref reliableToSend, 0);
					Interlocked.Exchange(ref unreliableToSend, 0);
				};

				client.DataReceived += (sender, data) => {
					if (data.SendOption == SendOption.Reliable) {
						Interlocked.Increment(ref clientsReliableReceived);
						Interlocked.Add(ref clientsReliableBytesReceived, data.Bytes.Length);
					} else if (data.SendOption == SendOption.None) {
						Interlocked.Increment(ref clientsUnreliableReceived);
						Interlocked.Add(ref clientsUnreliableBytesReceived, data.Bytes.Length);
					}

					data.Recycle();
				};

				bool connected = false;

				while (processActive) {
					if (!connected && client.State == Hazel.ConnectionState.Connected) {
						connected = true;
						Interlocked.Increment(ref clientsConnectedCount);
						Interlocked.Exchange(ref reliableToSend, reliableMessages);
						Interlocked.Exchange(ref unreliableToSend, unreliableMessages);
					}

					Thread.Sleep(1000 / clientTickRate);
				}

				client.Close();
			}, TaskCreationOptions.LongRunning);
		}
	}

	public sealed class PhotonBenchmark : BenchmarkNet {
		private class PhotonPeerListener : IPhotonPeerListener {
			public event Action OnConnected;
			public event Action OnDisconnected;
			public event Action<byte[]> OnReliableReceived;
			public event Action<byte[]> OnUnreliableReceived;

			public void OnMessage(object message) {
				byte[] data = (byte[])message;

				if (data[0] == messageData[0])
					OnReliableReceived(data);
				else if (data[0] == reversedData[0])
					OnUnreliableReceived(data);
			}

			public void OnStatusChanged(StatusCode statusCode) {
				switch (statusCode) {
					case StatusCode.Connect:
						OnConnected();

						break;

					case StatusCode.Disconnect:
						OnDisconnected();

						break;
				}
			}

			public void OnEvent(EventData netEvent) { }

			public void OnOperationResponse(OperationResponse operationResponse) { }

			public void DebugReturn(DebugLevel level, string message) { }
		}

		public static void Server() {
			PhotonPeerListener listener = new PhotonPeerListener();
			PhotonPeer server = new PhotonPeer(listener, ConnectionProtocol.Udp);

			server.Connect(ip + ":" + port, title);

			listener.OnConnected += () => {
				Thread.Sleep(Timeout.Infinite);
			};

			listener.OnDisconnected += () => {
				processFailure = true;
			};

			while (processActive) {
				server.Service();
				Thread.Sleep(1000 / serverTickRate);
			}

			server.Disconnect();
		}

		public static async Task Client() {
			await Task.Factory.StartNew(() => {
				PhotonPeerListener listener = new PhotonPeerListener();
				PhotonPeer client = new PhotonPeer(listener, ConnectionProtocol.Udp);

				client.Connect(ip + ":" + port, title);

				int reliableToSend = 0;
				int unreliableToSend = 0;
				int reliableSentCount = 0;
				int unreliableSentCount = 0;

				Task.Factory.StartNew(async() => {
					bool reliableIncremented = false;
					bool unreliableIncremented = false;

					while (processActive) {
						if (reliableToSend > 0) {
							client.SendMessage(messageData, true, 0, false);
							Interlocked.Decrement(ref reliableToSend);
							Interlocked.Increment(ref reliableSentCount);
							Interlocked.Increment(ref clientsReliableSent);
							Interlocked.Add(ref clientsReliableBytesSent, messageData.Length);
						}

						if (unreliableToSend > 0) {
							client.SendMessage(reversedData, false, 1, false);
							Interlocked.Decrement(ref unreliableToSend);
							Interlocked.Increment(ref unreliableSentCount);
							Interlocked.Increment(ref clientsUnreliableSent);
							Interlocked.Add(ref clientsUnreliableBytesSent, reversedData.Length);
						}

						if (reliableToSend > 0 && !reliableIncremented) {
							reliableIncremented = true;
							Interlocked.Increment(ref clientsStreamsCount);
						} else if (reliableToSend == 0 && reliableIncremented) {
							reliableIncremented = false;
							Interlocked.Decrement(ref clientsStreamsCount);
						}

						if (unreliableToSend > 0 && !unreliableIncremented) {
							unreliableIncremented = true;
							Interlocked.Increment(ref clientsStreamsCount);
						} else if (unreliableToSend == 0 && unreliableIncremented) {
							unreliableIncremented = false;
							Interlocked.Decrement(ref clientsStreamsCount);
						}

						await Task.Delay(1000 / sendRate);
					}
				}, TaskCreationOptions.AttachedToParent);

				listener.OnConnected += () => {
					Interlocked.Increment(ref clientsConnectedCount);
					Interlocked.Exchange(ref reliableToSend, reliableMessages);
					Interlocked.Exchange(ref unreliableToSend, unreliableMessages);
				};

				listener.OnDisconnected += () => {
					Interlocked.Increment(ref clientsDisconnectedCount);
					Interlocked.Exchange(ref reliableToSend, 0);
					Interlocked.Exchange(ref unreliableToSend, 0);
				};

				listener.OnReliableReceived += (data) => {
					Interlocked.Increment(ref clientsReliableReceived);
					Interlocked.Add(ref clientsReliableBytesReceived, data.Length);
				};

				listener.OnUnreliableReceived += (data) => {
					Interlocked.Increment(ref clientsUnreliableReceived);
					Interlocked.Add(ref clientsUnreliableBytesReceived, data.Length);
				};

				while (processActive) {
					client.Service();
					Thread.Sleep(1000 / clientTickRate);
				}

				client.Disconnect();
			}, TaskCreationOptions.LongRunning);
		}
	}

	public sealed class NeutrinoBenchmark : BenchmarkNet {
		private class ReliableMessage : NetworkMessage {
			public ReliableMessage() {
				IsGuaranteed = true;
			}

			[MsgPack.Serialization.MessagePackMember(0)]
			public string Message {
				get; set;
			}
		}

		private class UnreliableMessage : NetworkMessage {
			public UnreliableMessage() {
				IsGuaranteed = false;
			}

			[MsgPack.Serialization.MessagePackMember(0)]
			public string Message {
				get; set;
			}
		}

		public static void Server() {
			Node server = new Node(port, typeof(NeutrinoBenchmark).Assembly);

			NeutrinoConfig.PeerTimeoutMillis = ((Math.Max(reliableMessages, unreliableMessages) / sendRate) * 2) * 1000;

			server.OnReceived += (message) => {
				NetworkPeer peer = message.Source;

				if (message is ReliableMessage) {
					ReliableMessage reliableMessage = message as ReliableMessage;
					Interlocked.Increment(ref serverReliableReceived);
					Interlocked.Add(ref serverReliableBytesReceived, reliableMessage.Message.Length);
					peer.SendNetworkMessage(server.GetMessage<ReliableMessage>());
					Interlocked.Increment(ref serverReliableSent);
					Interlocked.Add(ref serverReliableBytesSent, messageData.Length);
				} else if (message is UnreliableMessage) {
					UnreliableMessage unreliableMessage = message as UnreliableMessage;
					Interlocked.Increment(ref serverUnreliableReceived);
					Interlocked.Add(ref serverUnreliableBytesReceived, unreliableMessage.Message.Length);
					peer.SendNetworkMessage(server.GetMessage<UnreliableMessage>());
					Interlocked.Increment(ref serverUnreliableSent);
					Interlocked.Add(ref serverUnreliableBytesSent, reversedData.Length);
				}
			};

			server.Start();

			while (processActive) {
				server.Update();
				Thread.Sleep(1000 / serverTickRate);
			}
		}

		public static async Task Client() {
			await Task.Factory.StartNew(() => {
				Node client = new Node(Task.CurrentId.ToString(), ip, port, typeof(NeutrinoBenchmark).Assembly);

				NeutrinoConfig.PeerTimeoutMillis = ((Math.Max(reliableMessages, unreliableMessages) / sendRate) * 2) * 1000;

				int reliableToSend = 0;
				int unreliableToSend = 0;
				int reliableSentCount = 0;
				int unreliableSentCount = 0;

				Task.Factory.StartNew(async() => {
					bool reliableIncremented = false;
					bool unreliableIncremented = false;

					while (processActive) {
						if (reliableToSend > 0) {
							ReliableMessage reliableMessage = client.GetMessage<ReliableMessage>();
							reliableMessage.Message = message;
							client.SendToAll(reliableMessage);
							Interlocked.Decrement(ref reliableToSend);
							Interlocked.Increment(ref reliableSentCount);
							Interlocked.Increment(ref clientsReliableSent);
							Interlocked.Add(ref clientsReliableBytesSent, messageData.Length);
						}

						if (unreliableToSend > 0) {
							UnreliableMessage unreliableMessage = client.GetMessage<UnreliableMessage>();
							unreliableMessage.Message = message;
							client.SendToAll(unreliableMessage);
							Interlocked.Decrement(ref unreliableToSend);
							Interlocked.Increment(ref unreliableSentCount);
							Interlocked.Increment(ref clientsUnreliableSent);
							Interlocked.Add(ref clientsUnreliableBytesSent, reversedData.Length);
						}

						if (reliableToSend > 0 && !reliableIncremented) {
							reliableIncremented = true;
							Interlocked.Increment(ref clientsStreamsCount);
						} else if (reliableToSend == 0 && reliableIncremented) {
							reliableIncremented = false;
							Interlocked.Decrement(ref clientsStreamsCount);
						}

						if (unreliableToSend > 0 && !unreliableIncremented) {
							unreliableIncremented = true;
							Interlocked.Increment(ref clientsStreamsCount);
						} else if (unreliableToSend == 0 && unreliableIncremented) {
							unreliableIncremented = false;
							Interlocked.Decrement(ref clientsStreamsCount);
						}

						await Task.Delay(1000 / sendRate);
					}
				}, TaskCreationOptions.AttachedToParent);

				client.OnPeerConnected += (peer) => {
					Interlocked.Increment(ref clientsConnectedCount);
					Interlocked.Exchange(ref reliableToSend, reliableMessages);
					Interlocked.Exchange(ref unreliableToSend, unreliableMessages);
				};

				client.OnPeerDisconnected += (peer) => {
					Interlocked.Increment(ref clientsDisconnectedCount);
					Interlocked.Exchange(ref reliableToSend, 0);
					Interlocked.Exchange(ref unreliableToSend, 0);
				};

				client.OnReceived += (message) => {
					if (message is ReliableMessage) {
						ReliableMessage reliableMessage = message as ReliableMessage;
						Interlocked.Increment(ref clientsReliableReceived);
						Interlocked.Add(ref clientsReliableBytesReceived, reliableMessage.Message.Length);
					} else if (message is UnreliableMessage) {
						UnreliableMessage unreliableMessage = message as UnreliableMessage;
						Interlocked.Increment(ref clientsUnreliableReceived);
						Interlocked.Add(ref clientsUnreliableBytesReceived, unreliableMessage.Message.Length);
					}
				};

				client.Start();

				while (processActive) {
					client.Update();
					Thread.Sleep(1000 / clientTickRate);
				}
			}, TaskCreationOptions.LongRunning);
		}
	}

	public sealed class DarkRiftBenchmark : BenchmarkNet {
		public static void Server() {
			DarkRiftServer server = new DarkRiftServer(new ServerSpawnData(IPAddress.Parse(ip), port, IPVersion.IPv4));

			server.ClientManager.ClientConnected += (peer, netEvent) => {
				netEvent.Client.MessageReceived += (sender, data) => {
					using (Message message = data.GetMessage()) {
						using (DarkRiftReader reader = message.GetReader()) {
							if (data.SendMode == SendMode.Reliable) {
								Interlocked.Increment(ref serverReliableReceived);
								Interlocked.Add(ref serverReliableBytesReceived, reader.Length);

								using (DarkRiftWriter writer = DarkRiftWriter.Create(messageData.Length)) {
									writer.WriteRaw(messageData, 0, messageData.Length);

									using (Message reliableMessage = Message.Create(0, writer))
										data.Client.SendMessage(reliableMessage, SendMode.Reliable);
								}

								Interlocked.Increment(ref serverReliableSent);
								Interlocked.Add(ref serverReliableBytesSent, messageData.Length);
							} else if (data.SendMode == SendMode.Unreliable) {
								Interlocked.Increment(ref serverUnreliableReceived);
								Interlocked.Add(ref serverUnreliableBytesReceived, reader.Length);

								using (DarkRiftWriter writer = DarkRiftWriter.Create(reversedData.Length)) {
									writer.WriteRaw(reversedData, 0, reversedData.Length);

									using (Message unreliableMessage = Message.Create(0, writer))
										data.Client.SendMessage(unreliableMessage, SendMode.Unreliable);
								}

								Interlocked.Increment(ref serverUnreliableSent);
								Interlocked.Add(ref serverUnreliableBytesSent, reversedData.Length);
							}
						}
					}
				};
			};

			server.Start();

			while (processActive) {
				server.ExecuteDispatcherTasks();
				Thread.Sleep(1000 / serverTickRate);
			}
		}

		public static async Task Client() {
			await Task.Factory.StartNew(() => {
				DarkRiftClient client = new DarkRiftClient();

				client.Connect(IPAddress.Parse(ip), port, IPVersion.IPv4);

				int reliableToSend = 0;
				int unreliableToSend = 0;
				int reliableSentCount = 0;
				int unreliableSentCount = 0;

				Task.Factory.StartNew(async() => {
					bool reliableIncremented = false;
					bool unreliableIncremented = false;

					while (processActive) {
						if (reliableToSend > 0) {
							using (DarkRiftWriter writer = DarkRiftWriter.Create(messageData.Length)) {
								writer.WriteRaw(messageData, 0, messageData.Length);

								using (Message message = Message.Create(0, writer))
									client.SendMessage(message, SendMode.Reliable);
							}

							Interlocked.Decrement(ref reliableToSend);
							Interlocked.Increment(ref reliableSentCount);
							Interlocked.Increment(ref clientsReliableSent);
							Interlocked.Add(ref clientsReliableBytesSent, messageData.Length);
						}

						if (unreliableToSend > 0) {
							using (DarkRiftWriter writer = DarkRiftWriter.Create(reversedData.Length)) {
								writer.WriteRaw(reversedData, 0, reversedData.Length);

								using (Message message = Message.Create(0, writer))
									client.SendMessage(message, SendMode.Unreliable);
							}

							Interlocked.Decrement(ref unreliableToSend);
							Interlocked.Increment(ref unreliableSentCount);
							Interlocked.Increment(ref clientsUnreliableSent);
							Interlocked.Add(ref clientsUnreliableBytesSent, reversedData.Length);
						}

						if (reliableToSend > 0 && !reliableIncremented) {
							reliableIncremented = true;
							Interlocked.Increment(ref clientsStreamsCount);
						} else if (reliableToSend == 0 && reliableIncremented) {
							reliableIncremented = false;
							Interlocked.Decrement(ref clientsStreamsCount);
						}

						if (unreliableToSend > 0 && !unreliableIncremented) {
							unreliableIncremented = true;
							Interlocked.Increment(ref clientsStreamsCount);
						} else if (unreliableToSend == 0 && unreliableIncremented) {
							unreliableIncremented = false;
							Interlocked.Decrement(ref clientsStreamsCount);
						}

						await Task.Delay(1000 / sendRate);
					}
				}, TaskCreationOptions.AttachedToParent);

				client.Disconnected += (sender, data) => {
					Interlocked.Increment(ref clientsDisconnectedCount);
					Interlocked.Exchange(ref reliableToSend, 0);
					Interlocked.Exchange(ref unreliableToSend, 0);
				};

				client.MessageReceived += (sender, data) => {
					using (Message message = data.GetMessage()) {
						using (DarkRiftReader reader = message.GetReader()) {
							if (data.SendMode == SendMode.Reliable) {
								Interlocked.Increment(ref clientsReliableReceived);
								Interlocked.Add(ref clientsReliableBytesReceived, reader.Length);
							} else if (data.SendMode == SendMode.Unreliable) {
								Interlocked.Increment(ref clientsUnreliableReceived);
								Interlocked.Add(ref clientsUnreliableBytesReceived, reader.Length);
							}
						}
					}
				};

				bool connected = false;

				while (processActive) {
					if (!connected && client.Connected) {
						connected = true;
						Interlocked.Increment(ref clientsConnectedCount);
						Interlocked.Exchange(ref reliableToSend, reliableMessages);
						Interlocked.Exchange(ref unreliableToSend, unreliableMessages);
					}

					Thread.Sleep(1000 / clientTickRate);
				}
			}, TaskCreationOptions.LongRunning);
		}
	}
}
