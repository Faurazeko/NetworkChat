using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NetworkChat
{
    public enum MsgType
    {
        Hello = 0,
        Text = 1,
        File = 2
    }

    class NetworkChatClient
    {
        private const string ip = "192.168.0.69";
        private const int port = 1337;
        static TcpClient client;
        static NetworkStream Stream;

        const string dir = @"C:\FaurazekoChatData\";

        static Dictionary<int, string> usersSheet = new Dictionary<int, string>();

        static BinaryReader reader;
        static BinaryWriter writer;

        static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            Console.WriteLine("C# chat client by Faurazeko\nPlease wait...");

            try
            {
                client = new TcpClient();
                client.Connect(ip, port);
                Stream = client.GetStream();

                reader = new BinaryReader(Stream, Encoding.UTF8);
                writer = new BinaryWriter(Stream, Encoding.UTF8);

                Console.Write("Enter your name (max 64 symbols): ");
                string UserName = Console.ReadLine().Truncate(64);

                SendData(Encoding.UTF8.GetBytes(UserName), MsgType.Hello);

                Thread listenThread = new Thread( () => 
                {
                    while (usersSheet.Count == 0)
                    {
                        List<byte> data = new List<byte>();
                        var type = ReceiveData(data, out int ID, out string ext);

                        if(type == MsgType.Hello)
                        {
                            try
                            {
                                usersSheet = data.ToArray().Deserialize<Dictionary<int, string>>();
                            }
                            catch (Exception) {}
                        }
                    }
                    Console.WriteLine("Connected users:");
                    foreach (var item in usersSheet)
                        Console.WriteLine($"[{item.Key}] {item.Value}");

                    while (true)
                    {
                        List<byte> data = new List<byte>();
                        var type = ReceiveData(data, out int ID, out string ext);

                        if (!Directory.Exists(dir))
                            Directory.CreateDirectory(dir);

                        Random rand = new Random();
                        int randNum = rand.Next(0, int.MaxValue);

                        string fileDir = dir + randNum + ext;

                        usersSheet.TryGetValue(ID, out string senderName);

                        switch (type)
                        {
                            case MsgType.Hello:
                                MemoryStream ms = new MemoryStream(data.ToArray());
                                byte[] nickBytes = new byte[ms.Length-1];
                                ms.Read(nickBytes, 0, (int)(ms.Length-1));
                                string name = Encoding.UTF8.GetString(nickBytes);
                                int action = data.Last();

                                if(action == 1)
                                    usersSheet.Remove(ID);
                                else if(action == 0)
                                    usersSheet.Add(ID, name);

                                ms.Dispose();
                                break;
                            case MsgType.Text:
                                Console.WriteLine($"[{ID}] {senderName} : " + Encoding.UTF8.GetString(data.ToArray()));
                                break;
                            case MsgType.File:
                                using (var output = File.Create(fileDir))
                                {
                                    output.Write(data.ToArray());
                                }
                                Console.WriteLine($"[{ID}] {senderName} sended {ext} file. Path: {fileDir} ");
                                break;
                            default:
                                break;
                        }
                    }
                });
                listenThread.Start();

                while (true)
                {
                    var msg = Console.ReadLine();

                    if (msg == string.Empty)
                        continue;

                    if(msg.Contains(" "))
                    {
                        var splitted = msg.Trim().Split();

                        switch (splitted[0])
                        {
                            case "/file":

                                FileInfo fileInfo = new FileInfo(splitted[1]);

                                if (!fileInfo.Exists)
                                {
                                    Console.WriteLine("File does not exist.");
                                    continue;
                                }

                                if (fileInfo.Length > int.MaxValue)
                                {
                                    Console.WriteLine("Too big file. Max file size is 2 GB.");
                                    continue;
                                }

                                SendInfoPackage(MsgType.File, (int)fileInfo.Length, fileInfo.Extension.ToString());
                                client.Client.SendFile(splitted[1]);
                                continue;
                            default:
                                break;
                        }
                    }
                    SendData(Encoding.UTF8.GetBytes(msg), MsgType.Text);

                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message + '\n');
                Console.WriteLine(e.StackTrace);
                Console.ReadLine();
            }
        }

        static private MsgType ReceiveData(List<byte> Data, out int senderID, out string extension)
        {
            extension = "";

            while (!Stream.DataAvailable)
                Thread.Sleep(100);

            MsgType type = (MsgType)Enum.Parse(typeof(MsgType), reader.ReadByte().ToString());
            if (type == MsgType.File)
            {
                int extensionLen = reader.ReadInt32();
                extension = new string(reader.ReadChars(extensionLen));
            }

            int len = reader.ReadInt32();
            senderID = reader.ReadInt32();

            int needToRead = len;

            while (needToRead > 0)
            {
                while (!Stream.DataAvailable)
                    Thread.Sleep(100);

                int toRead = client.Available;

                if (toRead > needToRead)
                    toRead = needToRead;

                needToRead -= toRead;

                Data.AddRange(reader.ReadBytes(toRead));
            }

            return type;
        }

        //for text or hello
        static void SendData(byte[] data, MsgType type)
        {

            SendInfoPackage(type, data.Length, "");

            writer.Write(data);
        }


        static void SendInfoPackage(MsgType type, int dataLength, string extension)
        {
            List<byte> infoPacket = new List<byte>();
            infoPacket.Add((byte)(int)type);
            if (type == MsgType.File)
            {
                infoPacket.AddRange(BitConverter.GetBytes(extension.Length));
                infoPacket.AddRange(Encoding.UTF8.GetBytes(extension));
            }
            infoPacket.AddRange(BitConverter.GetBytes(dataLength));

            writer.Write(infoPacket.ToArray());
        }
    }

    public static class ExtendedSerializerExtensions
    {
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
        };

        public static byte[] Serialize<T>(this T source)
        {
            var asString = JsonConvert.SerializeObject(source, SerializerSettings);
            return Encoding.UTF8.GetBytes(asString);
        }

        public static T Deserialize<T>(this byte[] source)
        {
            var asString = Encoding.UTF8.GetString(source);
            return JsonConvert.DeserializeObject<T>(asString);
        }
    }

    public static class StringExt
    {
        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}