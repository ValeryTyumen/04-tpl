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
				var listener = new Listener(port, "method", OnContextAsync);
				listener.Start();

				log.InfoFormat("Proxy server started!");
				new ManualResetEvent(false).WaitOne();
			}
			catch (Exception e)
			{
				log.Fatal(e);
				throw;
			}
		}

		private static void SetHashServers()
		{
			hashServers = File.ReadAllLines(serverInfoFile);
		}

		private static async Task OnContextAsync(HttpListenerContext context)
		{
			var requestId = Guid.NewGuid();
			var query = context.Request.QueryString["query"];
			var remoteEndPoint = context.Request.RemoteEndPoint;
			log.InfoFormat("{0}: received {1} from {2}", requestId, query, remoteEndPoint);
			context.Request.InputStream.Close();

			var hash = await MakeRequest(query, hashServers[random.Next(hashServers.Length - 1)]);
			var encryptedBytes = Encoding.UTF8.GetBytes(hash);

			await context.Response.OutputStream.WriteAsync(encryptedBytes, 0, encryptedBytes.Length);
			context.Response.OutputStream.Close();
			log.InfoFormat("{0}: {1} sent back to {2}", requestId, hash, remoteEndPoint);
		}

		private static Task<string> MakeRequest(string query, string server)
		{
			return Task.Run(() =>
			{
				var requestUrl = String.Format("http://{0}/method?query={1}", server, query);
				var html = "";
				using (var client = new WebClient())
				{
					html = client.DownloadString(requestUrl);
				}
				return html;
			});
		}

		private const int port = 31337;
		private static readonly ILog log = LogManager.GetLogger(typeof(Program));
		private static string[] hashServers;
		private const string serverInfoFile = "HashServers.txt";
		private static readonly Random random = new Random();
	}
}
