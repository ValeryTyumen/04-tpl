using System;
﻿using System.Linq;
﻿using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using log4net.Config;

namespace HashServer
{
    class Program
    {
        private static int delayMs = 1000;
        private static int port = 20000;
        static void Main(string[] args)
        {
            if (args.Any())
                int.TryParse(args[0], out port);

            XmlConfigurator.Configure();
            try
            {
                var listener = new Listener(port, "method", OnContextAsync);
                listener.Start();

                var listenerSync = new ListenerSync(port, "methodSync", OnContext);
                listenerSync.Start();

                log.InfoFormat("Server started listening on port {0}!", port);
                Console.WriteLine("Enter server delay in ms");
                while (true)
                {
                    var timeToSleepString = Console.ReadLine();
                    int timeToSleep;
                    if (int.TryParse(timeToSleepString, out timeToSleep))
                        delayMs = timeToSleep;
                    else
                        Console.WriteLine("Couldn't parse \"{0}\" as valid int.", timeToSleepString);
                    log.InfoFormat("Delay is {0} ms", delayMs);
                }
            }
            catch (Exception e)
            {
                log.Fatal(e);
                throw;
            }
        }

        private static async Task OnContextAsync(HttpListenerContext context)
        {
            var requestId = Guid.NewGuid();
            var query = context.Request.QueryString["query"];
            var remoteEndPoint = context.Request.RemoteEndPoint;
            log.DebugFormat("{0}: received {1} from {2}", requestId, query, remoteEndPoint);
            context.Request.InputStream.Close();

            await Task.Delay(delayMs);

            var hash = Convert.ToBase64String(CalcHash(Encoding.UTF8.GetBytes(query)));
            var encryptedBytes = Encoding.UTF8.GetBytes(hash);

            await context.Response.OutputStream.WriteAsync(encryptedBytes, 0, encryptedBytes.Length);
            context.Response.OutputStream.Close();
            log.DebugFormat("{0}: {1} sent back to {2}", requestId, hash, remoteEndPoint);
        }

        private static void OnContext(HttpListenerContext context)
        {
            var requestId = Guid.NewGuid();
            var query = context.Request.QueryString["query"];
            var remoteEndPoint = context.Request.RemoteEndPoint;
            log.DebugFormat("{0}: received {1} from {2}", requestId, query, remoteEndPoint);
            context.Request.InputStream.Close();

            Thread.Sleep(delayMs);

            var hash = Convert.ToBase64String(CalcHash(Encoding.UTF8.GetBytes(query)));
            var encryptedBytes = Encoding.UTF8.GetBytes(hash);

            context.Response.OutputStream.WriteAsync(encryptedBytes, 0, encryptedBytes.Length);
            context.Response.OutputStream.Close();
            log.DebugFormat("{0}: {1} sent back to {2}", requestId, hash, remoteEndPoint);
        }

        private static byte[] CalcHash(byte[] data)
        {
            using (var hasher = new HMACMD5(Key))
                return hasher.ComputeHash(data);
        }

        private static readonly byte[] Key = Encoding.UTF8.GetBytes("Контур.Шпора");
        private static readonly ILog log = LogManager.GetLogger(typeof(Program));
    }
}
