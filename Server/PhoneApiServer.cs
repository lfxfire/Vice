using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Vice.Server
{
    // Outcome of a command, sent back to the phone
    public class CommandResult
    {
        public bool Ok { get; private set; }
        public string Error { get; private set; }

        public static CommandResult Success() => new CommandResult() { Ok = true };
        public static CommandResult Failure(string error) => new CommandResult() { Ok = false, Error = error };
    }

    // Small HTTP API the Vice Remote phone app talks to. It's meant to be reached over Tailscale,
    // and every /api request must carry the pairing code as "Authorization: Bearer <code>".
    //
    //   GET  /            -> "Vice is running" (no pairing code needed, for testing the connection)
    //   GET  /api/state   -> { ok, state }
    //   POST /api/command -> body { command, value, value2 }, the same three lines as the command file
    //                     -> { ok, error, state }
    public class PhoneApiServer
    {
        public const string Version = "1";

        private const int MaxBodyBytes = 8192;

        private readonly int Port;
        private readonly string Token;
        private readonly Func<string, string, string, CommandResult> Execute;
        private readonly Func<object> GetState;
        private readonly Action<string> Log;

        private HttpListener Listener;
        private Thread ListenThread;

        public PhoneApiServer(int port, string token, Func<string, string, string, CommandResult> execute, Func<object> getState, Action<string> log)
        {
            Port = port;
            Token = NormaliseToken(token);
            Execute = execute;
            GetState = getState;
            Log = log;
        }

        public string Prefix => "http://+:" + Port + "/";

        public bool IsRunning => Listener != null && Listener.IsListening;

        // Starts listening on every interface. Returns false with accessDenied set when Windows
        // hasn't granted the URL reservation yet (see GrantAccess)
        public bool Start(out bool accessDenied)
        {
            accessDenied = false;

            try
            {
                Listener = new HttpListener();
                Listener.Prefixes.Add(Prefix);
                Listener.Start();
            }
            catch (HttpListenerException ex)
            {
                // 5 = access denied, the URL reservation is missing
                accessDenied = ex.ErrorCode == 5;
                Log("Phone API could not start on port " + Port + ": " + ex.Message);
                Listener = null;
                return false;
            }

            ListenThread = new Thread(ListenLoop) { IsBackground = true, Name = "Phone API" };
            ListenThread.Start();

            Log("Phone API listening on port " + Port);
            return true;
        }

        public void Stop()
        {
            try
            {
                Listener?.Close();
            }
            catch { }

            Listener = null;
        }

        private void ListenLoop()
        {
            HttpListener listener = Listener;

            while (listener != null && listener.IsListening)
            {
                HttpListenerContext context;

                try
                {
                    context = listener.GetContext();
                }
                catch (Exception)
                {
                    // Thrown when the listener is closed
                    return;
                }

                ThreadPool.QueueUserWorkItem(_ => Handle(context));
            }
        }

        private void Handle(HttpListenerContext context)
        {
            try
            {
                HttpListenerRequest request = context.Request;
                string path = request.Url.AbsolutePath.TrimEnd('/');

                if (path == "")
                {
                    WriteText(context.Response, 200, "Vice is running");
                    return;
                }

                if (!path.StartsWith("/api"))
                {
                    WriteJson(context.Response, 404, new { ok = false, error = "Not found" });
                    return;
                }

                if (!IsAuthorised(request))
                {
                    // Slows down anyone guessing pairing codes
                    Thread.Sleep(1000);
                    WriteJson(context.Response, 401, new { ok = false, error = "Wrong pairing code" });
                    return;
                }

                if (path == "/api/state" && request.HttpMethod == "GET")
                {
                    WriteJson(context.Response, 200, new { ok = true, version = Version, state = GetState() });
                }
                else if (path == "/api/command" && request.HttpMethod == "POST")
                {
                    JObject body = ReadBody(request);

                    if (body == null)
                    {
                        WriteJson(context.Response, 400, new { ok = false, error = "Body must be JSON like {\"command\": \"Lock\"}" });
                        return;
                    }

                    CommandResult result = Execute(
                        (string)body["command"] ?? "",
                        (string)body["value"] ?? "",
                        (string)body["value2"] ?? "");

                    WriteJson(context.Response, result.Ok ? 200 : 400, new { ok = result.Ok, error = result.Error, state = GetState() });
                }
                else
                {
                    WriteJson(context.Response, 404, new { ok = false, error = "Not found" });
                }
            }
            catch (Exception ex)
            {
                Log("Phone API request failed: " + ex.Message);

                try
                {
                    WriteJson(context.Response, 500, new { ok = false, error = "Vice hit an error running that" });
                }
                catch { }
            }
        }

        private bool IsAuthorised(HttpListenerRequest request)
        {
            string header = request.Headers["Authorization"] ?? "";

            if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return false;

            return TokensMatch(NormaliseToken(header.Substring(7)), Token);
        }

        private static JObject ReadBody(HttpListenerRequest request)
        {
            if (!request.HasEntityBody || request.ContentLength64 > MaxBodyBytes)
                return null;

            string text;
            using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
            {
                // Chunked uploads don't say their length up front, so the limit is also applied while reading
                char[] buffer = new char[MaxBodyBytes + 1];
                int length = reader.ReadBlock(buffer, 0, buffer.Length);

                if (length > MaxBodyBytes)
                    return null;

                text = new string(buffer, 0, length);
            }

            try
            {
                return JObject.Parse(text);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static void WriteJson(HttpListenerResponse response, int status, object body)
        {
            Write(response, status, "application/json", JsonConvert.SerializeObject(body));
        }

        private static void WriteText(HttpListenerResponse response, int status, string body)
        {
            Write(response, status, "text/plain", body);
        }

        private static void Write(HttpListenerResponse response, int status, string contentType, string body)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            response.StatusCode = status;
            response.ContentType = contentType + "; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Close();
        }

        #region Pairing code

        // No 0/O, 1/I/L, so the code is easy to type on a phone
        private const string TokenAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        // Makes a new pairing code like "K7QP-XM2D-9HTR-B4WN-E6ZA" (about 99 bits of randomness)
        public static string NewToken()
        {
            var builder = new StringBuilder();
            byte[] buffer = new byte[1];

            using (var rng = new RNGCryptoServiceProvider())
            {
                while (builder.Length < 24)
                {
                    if (builder.Length % 5 == 4)
                    {
                        builder.Append('-');
                        continue;
                    }

                    rng.GetBytes(buffer);

                    // Rejects the top of the byte range so every character is equally likely
                    if (buffer[0] < 248)
                        builder.Append(TokenAlphabet[buffer[0] % TokenAlphabet.Length]);
                }
            }

            return builder.ToString();
        }

        // Ignores dashes, spaces and case so the code can be typed loosely
        public static string NormaliseToken(string token)
        {
            var builder = new StringBuilder();

            foreach (char c in token ?? "")
            {
                if (char.IsLetterOrDigit(c))
                    builder.Append(char.ToUpperInvariant(c));
            }

            return builder.ToString();
        }

        // Compares in constant time so response timing doesn't leak the code
        private static bool TokensMatch(string a, string b)
        {
            if (a.Length == 0 || a.Length != b.Length)
                return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];

            return diff == 0;
        }

        #endregion Pairing code

        #region Windows permissions

        // Asks Windows (with a UAC prompt) to let Vice listen on the port, and opens the firewall
        // for Tailscale addresses only. Only needed once per port. Returns false if the user said no
        public static bool GrantAccess(int port)
        {
            string rule = "\"Vice phone API\"";
            string commands =
                "netsh http delete urlacl url=http://+:" + port + "/ >nul 2>&1" +
                " & netsh http add urlacl url=http://+:" + port + "/ sddl=D:(A;;GX;;;WD)" +
                " & netsh advfirewall firewall delete rule name=" + rule + " >nul 2>&1" +
                " & netsh advfirewall firewall add rule name=" + rule + " dir=in action=allow protocol=TCP localport=" + port +
                " remoteip=100.64.0.0/10,fd7a:115c:a1e0::/48";

            var psi = new ProcessStartInfo("cmd.exe", "/c " + commands)
            {
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            try
            {
                using (Process process = Process.Start(psi))
                    process.WaitForExit();

                return true;
            }
            catch (Win32Exception)
            {
                // The UAC prompt was declined
                return false;
            }
        }

        #endregion Windows permissions
    }
}
