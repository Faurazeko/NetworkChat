using System;
using System.Threading;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Text;
using System.Linq;
using Newtonsoft.Json;

namespace NetworkChat
{
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
    static class NetworkChat
    {
        static Mutex printMutex = new Mutex();
        const ConsoleColor defaultForegroundColor = ConsoleColor.White;
        static public TcpListener tcpListener;
        const int port = 1337;
        static List<Client> users = new List<Client>();
        static public Dictionary<int, string> usersSheet = new Dictionary<int, string>();

        static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            tcpListener = new TcpListener(IPAddress.Any, port);
            tcpListener.Start();
            usersSheet.Add(0, "SERVER");
            PrintLine("The Server has just started. Waiting for connections...");

            while (true)
            {
                TcpClient tcpClient = tcpListener.AcceptTcpClient();
                Client clientHandler = new Client("null", tcpClient);
                Thread clientThread = new Thread(clientHandler.Start);
                clientThread.Start();
            }
        }

        static public void AddConnection(Client client)
        {
            if (users.Contains(client))
                return;

            users.Add(client);
            usersSheet.Add(client.ID, client.UserName);
            BroadcastData(usersSheet.Serialize(), MsgType.Hello, client, "");
            BroadcastData(Encoding.UTF8.GetBytes($"connected to our beautiful chat!!!"), MsgType.Text, client, "");
        }

        static public void RemoveConnection(Client client)
        {
            if (!users.Contains(client))
                return;

            users.Remove(client);
            usersSheet.Remove(client.ID);
            BroadcastData(Encoding.UTF8.GetBytes($"just disconnected :("), MsgType.Text, client, "");
            BroadcastData(usersSheet.Serialize(), MsgType.Hello, client, "");
        }

        static public void BroadcastData(byte[] data, MsgType type, Client sender, string extension)
        {
            List<Client> usersToRemove = new List<Client>();

            //TODO: exception handling
            if (type == MsgType.Text)
                PrintLine($"[{sender.ID}] {sender.UserName} : " + Encoding.UTF8.GetString(data));
            else if (type == MsgType.Hello)
                PrintLine("HELLO PACKAGE");
            else
                PrintLine($"[{extension}]  {data.Length} bytes from {sender.UserName} [{sender.ID}]");

            for (int i = 0; i < users.Count; i++)
            {
                if (sender == users[i] && type != MsgType.Hello)
                    continue;

                if (!SocketConnected(users[i].client.Client))
                {
                    usersToRemove.Add(users[i]);
                    continue;
                }

                if (sender != users[i] && type == MsgType.Hello)
                {
                    List<byte> packet = new List<byte>();

                    byte[] tempData = Encoding.UTF8.GetBytes(sender.UserName);

                    SendPacket(GetInfoPackage(type, tempData.Length+1, sender.ID, extension), users[i]);

                    packet.AddRange(tempData);
                    packet.Add((byte)(!users.Contains(sender) ? 1 : 0));

                    SendPacket(packet.ToArray(), users[i]);
                    continue;
                }

                MemoryStream ms = new MemoryStream(data);

                SendPacket(GetInfoPackage(type, data.Length, sender.ID, extension), users[i]);

                SendPacket(data.ToArray(), users[i]);

                ms.Dispose();
            }

            foreach (var item in usersToRemove)
                RemoveConnection(item);
        }

        static bool SendPacket(byte[] data, Client client)
        {
            if (client.writer == null)
                return false;

            client.writer.Write(data);

            return true;
        }

        static public bool SocketConnected(Socket s)
        {
            if (s == null)
                return false;

            bool firstState = s.Poll(1000, SelectMode.SelectRead);
            bool secondState = s.Available == 0;

            if (firstState && secondState)
                return false;
            else
                return true;
        }

        public static int GetNewID()
        {
            int result = new int();
            Random rand = new Random();
            do
            {
                result = rand.Next();
            } while (usersSheet.TryGetValue(result, out _));

            return result;
        }

        public static byte[] GetInfoPackage(MsgType type, int dataLen, int senderID, string ext)
        {
            List<byte> infoPacket = new List<byte>();
            infoPacket.Add((byte)(int)type);
            if (type == MsgType.File)
            {
                infoPacket.AddRange(BitConverter.GetBytes(ext.Length));
                infoPacket.AddRange(Encoding.UTF8.GetBytes(ext));
            }
            infoPacket.AddRange(BitConverter.GetBytes(dataLen));
            infoPacket.AddRange(BitConverter.GetBytes(senderID));
            return infoPacket.ToArray();
        }

        static public void Print(string str, ConsoleColor foregroundColor = defaultForegroundColor)
        {
            printMutex.WaitOne();

            Console.ForegroundColor = foregroundColor;
            Console.Write(str);

            Console.ForegroundColor = defaultForegroundColor;

            printMutex.ReleaseMutex();
        }
        static public void PrintLine(string str = "", ConsoleColor foregroundColor = defaultForegroundColor) => Print(str + "\n", foregroundColor);
    }
}
