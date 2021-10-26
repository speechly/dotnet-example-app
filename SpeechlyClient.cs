using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Grpc.Core;
using OpenTK.Audio.OpenAL;
using Speechly.Identity.V1;
using Speechly.Slu.V1;
using Google.Protobuf;

namespace SpeechlyExampleApp
{
    public class SpeechlyClient
    {
        string apiUrl = "https://api.speechly.com";
        bool debug;
        string appId;
        string deviceId;
        string token;
        Microphone mic;
        GrpcChannel channel;
        SLU.SLUClient sluClient;
        Identity.IdentityClient identityClient;
        AsyncDuplexStreamingCall<SLURequest, SLUResponse> call;

        Thread readThread;
        SLURequest configRequest = new SLURequest
        {
            Config = new SLUConfig
            {
                Encoding = SLUConfig.Types.Encoding.Linear16,
                SampleRateHertz = 16_000,
            }
        };

        SLURequest startRequest = new SLURequest
        {
            Event = new SLUEvent { Event = SLUEvent.Types.Event.Start }
        };

        SLURequest stopRequest = new SLURequest
        {
            Event = new SLUEvent { Event = SLUEvent.Types.Event.Stop }
        };

        public SpeechlyClient(string appId, bool debug = false)
        {
            this.appId = appId;
            this.debug = debug;
            this.deviceId = System.Guid.NewGuid().ToString();
            if (this.debug)
            {
                Console.WriteLine($"deviceId: {deviceId}");
            }
            this.mic = new Microphone(this);
            this.channel = GrpcChannel.ForAddress(apiUrl);
            this.identityClient = new Identity.IdentityClient(channel);
        }

        ~SpeechlyClient()
        {
            channel.ShutdownAsync().Wait();
        }

        private async Task<string> getToken()
        {
            var loginRquest = new LoginRequest
            {
                AppId = this.appId,
                DeviceId = this.deviceId
            };
            var response = await identityClient.LoginAsync(loginRquest);
            if (this.debug)
            {
                Console.WriteLine($"Token: {response.Token}");
            }
            return response.Token;
        }

        public async void initialize()
        {
            if (this.debug)
            {
                Console.WriteLine("initializing");
            }

            if (this.token == null)
            {
                this.token = await getToken();
            }

            var credentials = CallCredentials.FromInterceptor((context, metadata) =>
            {
                metadata.Add("Authorization", $"Bearer {this.token}");
                return Task.CompletedTask;
            });

            var channel = GrpcChannel.ForAddress(this.apiUrl, new GrpcChannelOptions
            {
                Credentials = ChannelCredentials.Create(new SslCredentials(), credentials)
            });

            this.sluClient = new SLU.SLUClient(channel);
            this.call = this.sluClient.Stream();

            ThreadStart childref = new ThreadStart(readResponse);
            readThread = new Thread(childref);
            readThread.Start();

            call.RequestStream.WriteAsync(configRequest).Wait();
        }

        private async void readResponse()
        {
            while (await call.ResponseStream.MoveNext())
            {
               var r = call.ResponseStream.Current;
               Console.WriteLine(r.ToString());
            }
        }

        public void start()
        {
            if (this.debug)
            {
                Console.WriteLine("starting");
            }

            sendStart();
            this.mic.start();
        }

        public void stop()
        {
            if (this.debug)
            {
                Console.WriteLine("stopping");
            }

            this.mic.stop();
        }

        public void toggle()
        {
            if (this.mic.Recording)
            {
                this.stop();
            }
            else
            {
                this.start();
            }
        }

        public void sendStart()
        {
            call.RequestStream.WriteAsync(startRequest).Wait();
        }

        public void sendStop()
        {
            call.RequestStream.WriteAsync(stopRequest).Wait();
        }

        public void sendAudio(byte[] audioChunk)
        {
            SLURequest audio = new SLURequest
            {
                Audio = ByteString.CopyFrom(audioChunk)
            };

            if (call is null || call.RequestStream is null)
            {
                Console.WriteLine("call is null");
                return;
            }

            try {
                call.RequestStream.WriteAsync(audio).Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine("Error sending data: " + e);
            }
        }
    }
}
