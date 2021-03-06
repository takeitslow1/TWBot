﻿using System.Net;
using System.IO;
using Renci.SshNet;
using System.Xml.Linq;
using System;
using System.Media;
//using AudioSwitcher.AudioApi.CoreAudio;

namespace TW_Bot
{

    public static class Client
    {
        public static SoundPlayer player = new SoundPlayer();
        public static int lastDespacito = 0;
        public static DateTime? lastServerCommunicationTime = null;
        //public static CoreAudioDevice defaultPlaybackDevice;//
        static Client()
        {
            player.SoundLocation = AppDomain.CurrentDomain.BaseDirectory + "\\jfladespa.wav";
          //  defaultPlaybackDevice = new CoreAudioController().DefaultPlaybackCommunicationsDevice;
        }
        private static Random random = new Random();

        public static string villagesXml = null;
        public static string configXml = null;
        public static string ourInstanceIdentifier = random.Next(0, 10000000).ToString();
        public static bool execute = false;

        // XML Parsed Config Vars:
        static bool activeBotExists = false;
        static bool scavEnabled = false;
        static bool farmEnabled = false;
        static int readingEnabled = 0;
        static bool pause = false;
        static string lastActiveId = "-1";

        public static void WaitForTurn()
        {
            if (!Settings.USE_SERVER)
            {
                execute = true;
                return;
            }
            if(lastServerCommunicationTime != null)
            {
                if(execute)
                {
                    TimeSpan timeSpan = DateTime.Now.Subtract((DateTime)lastServerCommunicationTime);
                    if (timeSpan.Minutes <= 5) return;
                }
            }
            DetermineFlow();
            if (execute)
            {
                System.Console.WriteLine("It is our turn!");
                return;
            }
            else
            {
                System.Console.WriteLine("Sleeping for 1 minute and then rechecking if it is our turn to run.");
                System.Threading.Thread.Sleep(1000 * 60);
                WaitForTurn();
            }
        }

        public static void DetermineFlow() // Ready
        {
            if (!Settings.USE_SERVER) return;
            lastServerCommunicationTime = DateTime.Now;
            System.Console.WriteLine("Downloading config.xml\nOur ID is: {0}", ourInstanceIdentifier);
            configXml = Download("http://lunarmerlin.asuscomm.com/twbot/users/" + Settings.USERNAME + "/" + Settings.WORLD + "/" + "config.xml");
            XDocument parsedConfig = ReadConfigXml(configXml);
            System.Console.WriteLine("Setting Farming To: {0}", farmEnabled);
            System.Console.WriteLine("Setting Scavenging To: {0}", scavEnabled);
            System.Console.WriteLine("Setting Report Read Interval To: {0}", readingEnabled);
            Settings.FA_FARMING_ENABLED = farmEnabled;
            Settings.SCAVENGING_ENABLED = scavEnabled;
            Settings.REPORT_READ_INTERVAL_MINUTES = readingEnabled;
            if(pause)
            {
                System.Console.WriteLine("Execution is paused in control panel.");
                if (ourInstanceIdentifier.Equals(lastActiveId)) Settings.NEXT_RESTART_IS_IMMEDIATE = true;
                else Settings.NEXT_RESTART_IS_IMMEDIATE = false;
                execute = false;
                return;
            }
            if(activeBotExists)
            {
                if(ourInstanceIdentifier.Equals(lastActiveId)) // Is it us?
                {
                    // it is us.
                    execute = true;
                }
                else if(!Settings.TAKE_CONTROL_ON_START)
                {
                    System.Console.WriteLine("Already exists other active client.");
                    // Other instance is running already.
                    execute = false;
                    return;
                }
            }
            if(!activeBotExists || Settings.TAKE_CONTROL_ON_START)//if(Settings.TAKE_CONTROL_ON_START)
            {
                if (Settings.TAKE_CONTROL_ON_START) System.Console.WriteLine("Taking Control even though already active bot exists.");
                Settings.TAKE_CONTROL_ON_START = false;
                System.Console.WriteLine("No other active client.");
                System.Console.WriteLine("Attempting to take ownership.");
                // No other active bot.
                // And not in pause.
                // Let's see if we can become the active one.
                
                // Prepare config XML, upload, and determine if we won.
                foreach (XElement element in parsedConfig.Descendants().Elements())
                {
                    switch (element.Name.LocalName)
                    {
                        case "progress":
                            element.SetValue(Settings.PROGRESS);
                            break;
                        case "isActive":
                            element.SetValue("1");
                            break;
                        case "lastActiveInstanceIdentifier":
                            element.SetValue(ourInstanceIdentifier);
                            break;
                        default:
                            //System.Console.WriteLine(element.Name.LocalName);
                            break;
                    }
                }
                System.Console.WriteLine("Uploading config.");
                UploadString(parsedConfig.ToString(), "/var/www/html/twbot/users/" + Settings.USERNAME + "/" + Settings.WORLD + "/" + "config.xml");
                System.Console.WriteLine("Uploaded config.");
                DetermineFlow();
            }
        }

        public static void DownloadAll() // Ready
        {
            if (!Settings.USE_SERVER) return;
            System.Console.WriteLine("Downloading all data.");
            villagesXml = Download("http://lunarmerlin.asuscomm.com/twbot/users/" + Settings.USERNAME + "/" + Settings.WORLD + "/" + "villages.xml");
            configXml = Download("http://lunarmerlin.asuscomm.com/twbot/users/" + Settings.USERNAME + "/" + Settings.WORLD + "/" + "config.xml");
            ReadConfigXml(configXml);
        }

        public static XDocument ReadConfigXml(string configXml)
        {
            var configXmlParsed = XDocument.Parse(configXml);
            foreach (XElement element in configXmlParsed.Descendants().Elements())
            {
                switch (element.Name.LocalName)
                {
                    case "isActive":
                        activeBotExists = element.Value == "1" ? true : false;
                        break;
                    case "scavengingEnabled":
                        scavEnabled = element.Value == "1" ? true : false;
                        break;
                    case "farmingAssistEnabled":
                        farmEnabled = element.Value == "1" ? true : false;
                        break;
                    case "reportReadingEnabled":
                        readingEnabled = int.Parse(element.Value);
                        break;
                    case "pause":
                        pause = element.Value == "1" ? true : false;
                        break;
                    case "lastActiveInstanceIdentifier":
                        lastActiveId = element.Value;
                        break;
                    case "despacito":
                        if (lastDespacito == 0 && int.Parse(element.Value) == 1)
                        {
                            lastDespacito = 1;
                            //defaultPlaybackDevice.Mute(true);
                            player.Play();
                            
                        }
                        else if (lastDespacito == 1 && int.Parse(element.Value) == 0)
                        {
                            player.Stop();
                            lastDespacito = 0;
                        }
                        break;
                    default:
                        //  System.Console.WriteLine(element.Name.LocalName);
                        break;
                }
            }
            return configXmlParsed;
        }

        public static string Download(string fileAddress)
        {
            if (!Settings.USE_SERVER) return "";
            System.Console.WriteLine("Downloading: " + fileAddress);
            string data = "";
            using (var client = new WebClient())
            {
                data = client.DownloadString(fileAddress);
            }
            return data;
        }

        public static void SaveAll() // Maybe not ready?
        {
            if (!Settings.USE_SERVER) return;
            File.WriteAllText(Settings.USERNAME + "/" + Settings.WORLD + "/" + "SERVER_villages.xml", villagesXml);
            //File.WriteAllText(Settings.USERNAME + "/" + Settings.WORLD + "/" + "villages.xml", villagesXml);
        }

        public static void UploadAll() // This function is ready for new web panel.
        {
            if (!Settings.USE_SERVER)
            {
                SaveAll();
                return;
            }
            //UploadString(villagesXml, "/var/www/html/twbot/villages.xml");
            UploadFile(Settings.USERNAME + "/" + Settings.WORLD + "/" + "villages.xml", "/var/www/html/twbot/users/" + Settings.USERNAME + "/" + Settings.WORLD + "/" + "villages.xml");
            UploadFile(Settings.USERNAME + "/" + Settings.WORLD + "/" + "latest.jpg", "/var/www/html/twbot/users/" + Settings.USERNAME + "/" + Settings.WORLD + "/" + "latest.jpg");
        }

        public static void UploadScreenCapture()
        {
            if (!Settings.USE_SERVER) return;
            UploadFile(Settings.USERNAME + "/" + Settings.WORLD + "/" + "latest.jpg", "/var/www/html/twbot/users/" + Settings.USERNAME + "/" + Settings.WORLD + "/" + "latest.jpg");
        }

        public static void UploadFile(string file, string fileAddress)
        {
            if (!Settings.USE_SERVER) return;
            System.Console.WriteLine("Uploading: " + file);
            FileInfo fileInfo = new FileInfo(file);
            FileStream fileStream = new FileStream(fileInfo.FullName, FileMode.Open);
            if (fileStream == null) return;
            ConnectionInfo connectionInfo = new ConnectionInfo("lunarmerlin.asuscomm.com", "merlin", new PasswordAuthenticationMethod("merlin", "bonxel140!#\""));
            using (var client = new SftpClient(connectionInfo))
            {

                client.Connect();
                client.BufferSize = 1024 * 1024 * 128;
                client.UploadFile(fileStream, fileAddress);
            }
            fileStream.Close();
            System.Console.WriteLine("Uploaded file.");
        }

        public static void UploadString(string data, string fileAddress)
        {
            if (!Settings.USE_SERVER) return;
            System.Console.WriteLine("Uploading: " + fileAddress);
            ConnectionInfo connectionInfo = new ConnectionInfo("lunarmerlin.asuscomm.com", "merlin", new PasswordAuthenticationMethod("merlin", "bonxel140!#\""));
            using (var client = new SftpClient(connectionInfo))
            {

                client.Connect();
                client.BufferSize = 1024 * 1024;
                client.UploadFile(GenerateStreamFromString(data), fileAddress);
            }
            System.Console.WriteLine("Uploaded file.");
        }

        // Stole this function from StackOverflow lol.
        // https://stackoverflow.com/questions/17833080/convert-string-to-filestream-in-c-sharp
        public static Stream GenerateStreamFromString(string s)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
    }
}