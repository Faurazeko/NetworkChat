using System;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Threading;

namespace NetworkChat
{
    public enum MsgType
    {
        Hello = 0,
        Text = 1,
        File = 2
    }

    public class Client
    {
        public int ID { get; private set; }
        public string UserName { get; private set; }
        public TcpClient client;
        public NetworkStream Stream;

        public BinaryReader reader;
        public BinaryWriter writer;

        public Client(string name, TcpClient tcpClient)
        {
            UserName = name;
            client = tcpClient;
            ID = NetworkChat.GetNewID();
        }

        public void Start()
        {
            try
            {
                Stream = client.GetStream();

                reader = new BinaryReader(Stream, Encoding.UTF8);
                writer = new BinaryWriter(Stream, Encoding.UTF8);

                while (true)
                {
                    if (!NetworkChat.SocketConnected(client.Client))
                    {
                        NetworkChat.RemoveConnection(this);
                        Stop();
                        return;
                    }

                    List<byte> data = new List<byte>();
                    MsgType type = ReceiveData(data, out string ext);
                    if(type == MsgType.Hello)
                    {
                        UserName = Encoding.UTF8.GetString(data.ToArray());
                        NetworkChat.AddConnection(this);
                        continue;
                    }

                    NetworkChat.BroadcastData(data.ToArray(), type, this, ext);
                }

            }
            catch (Exception e)
            {
                NetworkChat.PrintLine(e.Message + "\n\n\n" + e.StackTrace, ConsoleColor.Red);
                Stop();
            }
        }

        private MsgType ReceiveData(List<byte> Data, out string ext)
        {
            ext = "";

            while (!Stream.DataAvailable)
                Thread.Sleep(100);

            MsgType type = (MsgType)Enum.Parse(typeof(MsgType), reader.ReadByte().ToString());
            if (type == MsgType.File)
            {
                int extLen = reader.ReadInt32();
                ext = new string(reader.ReadChars(extLen));
            }
            int len = reader.ReadInt32();


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

        private void Stop()
        {
            if (client != null)
                client.Close();

            if (Stream != null)
                Stream.Close();

            writer = null;
            reader = null;

            NetworkChat.RemoveConnection(this);
            NetworkChat.PrintLine(ID + " " + UserName + " Disposed.");
        }
    }
}