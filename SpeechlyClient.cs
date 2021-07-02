using System;
using System.Threading;
using System.Threading.Tasks;
using Speechly.Identity.V1;
using Speechly.Slu.V1;
using OpenTK.Audio.OpenAL;
using Grpc.Net.Client;
using Grpc.Core;
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
                LanguageCode = "en-US",
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

            var authInterceptor = new AsyncAuthInterceptor(async (context, metadata) =>
            {
                metadata.Add(
                    new Metadata.Entry("Authorization", "Bearer " + this.token));
            });

            var metadataCredentials = CallCredentials.FromInterceptor(authInterceptor);
            ChannelCredentials channelCredentials = ChannelCredentials.Create(new SslCredentials(), metadataCredentials);
            Channel channel = new Channel("api.speechly.com", channelCredentials);
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

        public async void start()
        {
            if (this.debug)
            {
                Console.WriteLine("starting");
            }

            sendStart();
            this.mic.start();

        }

        public async void stop()
        {
            if (this.debug)
            {
                Console.WriteLine("stopping");
            }

            this.mic.stop();
        }

        public async void toggle()
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
