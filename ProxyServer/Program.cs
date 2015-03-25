using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using log4net.Config;
using HashServer;

namespace ProxyServer
{

	class Program
	{
		static void Main(string[] args)
		{
			XmlConfigurator.Configure();
			SetHashServers();
			try
			{
				var listener = new Listener(Port, "method", OnContextAsync);
				listener.Start();

				Log.InfoFormat("Proxy server started!");
				new ManualResetEvent(false).WaitOne();
			}
			catch (Exception e)
			{
				Log.Fatal(e);
				throw;
			}
		}

		private static void SetHashServers()
		{
			_hashServers = File.ReadAllLines(ServerInfoFile);
		}

		private static async Task OnContextAsync(HttpListenerContext context)
		{
			var requestId = Guid.NewGuid();
			var query = context.Request.QueryString["query"];
			var remoteEndPoint = context.Request.RemoteEndPoint;
			Log.InfoFormat("{0}: received {1} from {2}", requestId, query, remoteEndPoint);
			context.Request.InputStream.Close();
			string hashServer;
			do
			{
				hashServer = _hashServers[Random.Next(_hashServers.Length - 1)];
			} while (! (await GotResponseFromServer(query, hashServer, context.Response, requestId, remoteEndPoint)));
		}

		private static async Task<bool> GotResponseFromServer(string query, string server,
			HttpListenerResponse responseToClient, Guid requestId, IPEndPoint client)
		{
			var requestUrl = String.Format("http://{0}/method?query={1}", server, query);
			var requestToServer = WebRequest.Create(requestUrl);
			requestToServer.Timeout = Timeout;
			HttpWebResponse responseFromServer;
			try
			{
				responseFromServer = (HttpWebResponse)(await requestToServer.GetResponseAsync());
			}
			catch (WebException)
			{
				Log.InfoFormat("{0}: connection error with {1}", requestId, server);
				return false;
			}
			if (responseFromServer.StatusCode != HttpStatusCode.OK)
			{
				Log.InfoFormat("{0}: {1} response status is {2}", requestId, server, responseFromServer.StatusCode);
				return false;
			}
			var reader = new StreamReader(responseFromServer.GetResponseStream());
			var hash = reader.ReadToEnd();
			var encryptedBytes = Encoding.UTF8.GetBytes(hash);
			await responseToClient.OutputStream.WriteAsync(encryptedBytes, 0, encryptedBytes.Length);
			responseToClient.OutputStream.Close();
			Log.InfoFormat("{0}: {1} sent back to {2}", requestId, hash, client);
			return true;
		}

		private const int Port = 31337;
		private static readonly ILog Log = LogManager.GetLogger(typeof(Program));
		private static string[] _hashServers;
		private const string ServerInfoFile = "HashServers.txt";
		private static readonly Random Random = new Random();
		private const int Timeout = 3000;
	}
}
