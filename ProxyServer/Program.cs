using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
			var encodings = context.Request.Headers.GetValues("Accept-Encoding");
			var deflate = encodings != null && encodings.Contains("deflate");
			context.Request.InputStream.Close();
			bool gotResponse = false;
			Queue<string> serverQueue;
			lock(Random)
				serverQueue = new Queue<string>(_hashServers.OrderBy(z => Random.Next()));
			while (serverQueue.Count != 0)
			{
				var hashServer = serverQueue.Dequeue();
				if (await GotResponseFromServer(query, hashServer, context.Response, requestId, remoteEndPoint, deflate))
				{
					gotResponse = true;
					break;
				}
			}
			if (! gotResponse)
			{
				context.Response.StatusCode = 500;
				context.Response.Close();
			}
		}

		private static async Task<bool> GotResponseFromServer(string query, string server,
			HttpListenerResponse responseToClient, Guid requestId, IPEndPoint client, bool deflate)
		{
			var requestUrl = String.Format("http://{0}/method?query={1}", server, query);
			var requestToServer = CreateRequest(requestUrl);
			HttpWebResponse responseFromServer;
			var timeoutCheck = Task.Run(async () =>
				{ await Task.Delay(Timeout); return (WebResponse)null; });
			var responseGet = requestToServer.GetResponseAsync();
			try
			{
				var task = await Task.WhenAny(new [] { timeoutCheck, responseGet });
				if (task.Result == null)
				{
					Log.InfoFormat("{0}: {1} timeout", requestId, server);
					return false;
				}
				responseFromServer = (HttpWebResponse)task.Result;
			}
			catch (Exception)
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
			var stream = responseToClient.OutputStream;
			if (deflate)
			{
				responseToClient.Headers.Set("Content-Encoding", "deflate");
				stream = new DeflateStream(stream, CompressionLevel.Optimal);
			}
			await stream.WriteAsync(encryptedBytes, 0, encryptedBytes.Length);
			stream.Close();
			responseToClient.OutputStream.Close();
			Log.InfoFormat("{0}: {1} sent back to {2} from server {3}", requestId, hash, client, server);
			return true;
		}

		private static HttpWebRequest CreateRequest(string uriStr, int timeout = 30 * 1000)
		{
			var request = WebRequest.CreateHttp(uriStr);
			request.Timeout = timeout;
			request.Proxy = null;
			request.KeepAlive = true;
			request.ServicePoint.UseNagleAlgorithm = false;
			request.ServicePoint.ConnectionLimit = 10000;
			return request;
		}

		private const int Port = 31337;
		private static readonly ILog Log = LogManager.GetLogger(typeof(Program));
		private static string[] _hashServers;
		private const string ServerInfoFile = "HashServers.txt";
		private static readonly Random Random = new Random();
		private static readonly int Timeout = 3000;
	}
}
