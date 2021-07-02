using System;

namespace SpeechlyExampleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Example App.\nPress 'space' to toggle recording, press 'q' to quit");
            SpeechlyClient speechlyClient = new SpeechlyClient(appId: "your-app-id-here", debug: true);
            speechlyClient.initialize();

            while (!Console.KeyAvailable)
            {
                char key = Console.ReadKey().KeyChar;
                if (key == ' ')
                {
                    speechlyClient.toggle();
                }

                if (key == 'q')
                {
                    break;
                }
            }

            Console.WriteLine("The End");
        }
    }
}
