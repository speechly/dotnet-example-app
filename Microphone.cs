using System;
using System.Threading;
using System.Threading.Tasks;
using OpenTK.Audio.OpenAL;
using System.Collections.Generic;
using System.Linq;


namespace SpeechlyExampleApp
{
    public class Microphone
    {
        short freq = 16_000;
        short bufferSize = 16_000;
        ALCaptureDevice captureDevice;
        public bool Recording  // property
        { get; set; }

        Thread recordingThread;
        SpeechlyClient client;

        public Microphone(SpeechlyClient client)
        {
            this.client = client;
            List<string> captureDeviceList = ALC.GetStringList(GetEnumerationStringList.CaptureDeviceSpecifier).ToList();
            captureDevice = ALC.CaptureOpenDevice(captureDeviceList[0], freq, ALFormat.Mono16, bufferSize);
        }

        ~Microphone()
        {
            ALC.CaptureCloseDevice(captureDevice);
        }

        public void start()
        {
            this.Recording = true;
            ALC.CaptureStart(captureDevice);
            ThreadStart childref = new ThreadStart(readAudio);
            recordingThread = new Thread(childref);
            recordingThread.Start();
        }

        public void stop()
        {
            this.Recording = false;
            ALC.CaptureStop(captureDevice);
        }

        public void readAudio()
        {
            int samplesAvailable;
            while (this.Recording)
            {
                samplesAvailable = ALC.GetAvailableSamples(captureDevice);
                if (samplesAvailable > 0)
                {
                    short[] recordingBuffer = new short[samplesAvailable];
                    ALC.CaptureSamples(captureDevice, ref recordingBuffer[0], samplesAvailable);
                    byte[] audioChunk = new byte[samplesAvailable * 2];
                    for(short i = 0; i < samplesAvailable; i++)
                    {
                        byte[] bytes = BitConverter.GetBytes(recordingBuffer[i]);

                        audioChunk[i*2] = bytes[0];
                        audioChunk[i*2 + 1] = bytes[1];
                    }

                    client.sendAudio(audioChunk);
                    Thread.Sleep(100);
                }
            }
            Console.WriteLine("SEND STOP");
            client.sendStop();
        }
    }
}
