﻿// this is an adaptation of NUnitLite's TcpWriter.cs with an additional 
// overrides and with network-activity UI enhancement
// This code is a small modification of 
// https://github.com/spouliot/Touch.Unit/blob/master/NUnitLite/TouchRunner/TcpTextWriter.cs
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

#if __IOS__
using UIKit;
#endif

namespace BCLTests.TestRunner.Core {
	public class TcpTextWriter : TextWriter {
		
		TcpClient client;
		StreamWriter writer;
		
		static string SelectHostName (string[] names, int port, out TcpClient tcp_client)
		{
			if (names.Length == 0)
				return null;

			if (names.Length == 1)
				return names [0];

			object lock_obj = new object ();
			string result = null;
			int failures = 0;

			tcp_client = null;

			using (var evt = new ManualResetEvent (false)) {
				for (int i = names.Length - 1; i >= 0; i--) {
					var name = names [i];
					ThreadPool.QueueUserWorkItem ((v) =>
						{
							try {
								Console.WriteLine ($"TcpTextWriter: attempting connection to {name}:{port}");
								var client = new TcpClient (name, port);
								lock (lock_obj) {
									if (result == null) {
										tcp_client = client;
										result = name;
										Console.WriteLine ($"TcpTextWriter: successfully connected to {name}:{port}");
									}
								}
								evt.Set ();
							} catch (Exception ex) {
								Console.WriteLine ($"TcpTextWriter: failed connection to {name}:{port}: {ex.Message}");
								lock (lock_obj) {
									failures++;
									if (failures == names.Length)
										evt.Set ();
								}
							}
						});
				}

				// Wait for 1 success or all failures
				evt.WaitOne ();
			}

			return result;
		}

		public TcpTextWriter (string hostName, int port)
		{
			if ((port < 0) || (port > ushort.MaxValue))
				throw new ArgumentOutOfRangeException (nameof (port), $"Port must be between 0 and {ushort.MaxValue}" );

			if (hostName == null)
				throw new ArgumentNullException (nameof (hostName));

#if __IOS__
			UIApplication.SharedApplication.NetworkActivityIndicatorVisible = true;
#endif

			try {
				Port = port;
				HostName = SelectHostName (hostName.Split (','), port, out client);
				writer = new StreamWriter (client.GetStream ());
			}
			catch {
#if __IOS__
				UIApplication.SharedApplication.NetworkActivityIndicatorVisible = false;
#endif
				throw;
			}
		}
		
		public string HostName { get; private set; }
		
		public int Port { get; private set; }

		// we override everything that StreamWriter overrides from TextWriter

		public override System.Text.Encoding Encoding => Encoding.UTF8;

		public override void Close ()
		{
#if __IOS__
			UIApplication.SharedApplication.NetworkActivityIndicatorVisible = false;
#endif
			writer.Close ();
		}
		
		protected override void Dispose (bool disposing)
		{
			 writer.Dispose ();
		}

		public override void Flush ()
		{
			writer.Flush ();
		}

		// minimum to override - see http://msdn.microsoft.com/en-us/library/system.io.textwriter.aspx
		public override void Write (char value)
		{
			writer.Write (value);
		}
		
		public override void Write (char[] buffer)
		{
			 writer.Write (buffer);
		}
		
		public override void Write (char[] buffer, int index, int count)
		{
			writer.Write (buffer, index, count);
		}

		public override void Write (string value)
		{
			writer.Write (value);
		}
		
		// special extra override to ensure we flush data regularly

		public override void WriteLine ()
		{
			writer.WriteLine ();
			writer.Flush ();
		}
	}
}