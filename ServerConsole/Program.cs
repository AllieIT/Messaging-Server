using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;

namespace ServerConsole
{
    class Client
    {
        private TcpClient client;
        private BinaryReader reader;
        public BinaryWriter writer;
        public string name;
        public string lang;
        public string connectionTime;
        public Thread message;

        public Client(ref TcpClient client, ref BinaryReader streamReader, ref string name, string connectionTime, string lang)
        {
            this.lang = lang;
            this.client = client;
            this.name = name;
            this.connectionTime = connectionTime;
            reader = streamReader;
            writer = new BinaryWriter(client.GetStream());

            message = new Thread(() =>
            {
                while(true)
                {
                    try
                    {
                        String messageText = reader.ReadString();
                        if(messageText == "/disconnect")
                        {
                            writer.Write("SERVER_DISCONNECT");
                        }
                        else if (messageText == "/img")
                        {
                            int length = reader.ReadInt32();
                            byte[] byteImage = reader.ReadBytes(length);
                            Server.BroadcastImage(byteImage, this.name);
                        }
                        else
                        {
                            Console.WriteLine("[" + this.name + "] " + messageText);
                            Server.Broadcast(messageText, this.name);
                        }
                    }
                    catch (Exception ex)
                    {
                        Server.Delete(this);
                    }
                }
            });
            message.Start();
        }
    }

    class Server
    {
        static string address;
        static string SERVER_VERSION = "0.7";
        static int port;
        static TcpListener server;
        static List<string> names = new List<string>();
        static List<Client> users = new List<Client>();
        static Dictionary<string, Dictionary<string, string>> langDict = new Dictionary<string, Dictionary<string, string>>();

        static void Main(string[] args)
        {
            InitializeLang();
            Console.Write("Server Address: ");
            address = Console.ReadLine();
            Console.Write("Port: ");
            string line = Console.ReadLine();

            try
            {
                port = int.Parse(line);
                server = new TcpListener(new IPEndPoint(IPAddress.Parse(address), port));
                server.Start();
                Console.WriteLine("Starting server on [" + address + ":" + port + "] on version " + SERVER_VERSION + ". Waiting for Connection...");
            }
            catch (Exception ex)
            {
                server = new TcpListener(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1000));
                server.Start();
                Console.WriteLine("Wrong format. Starting server on [127.0.0.1:1000] on version " + SERVER_VERSION + ". Waiting for Connection...");
            }

            Thread waitForConnect = new Thread(() =>
            {
                while(true)
                {
                    TcpClient client = server.AcceptTcpClient();
                    IPEndPoint IP = (IPEndPoint)client.Client.RemoteEndPoint;
                    
                    BinaryReader reader = new BinaryReader(client.GetStream());
                    BinaryWriter writer = new BinaryWriter(client.GetStream());

                    if (reader.ReadString() != SERVER_VERSION)
                    {
                        Console.WriteLine("Client disconnected: Outdated client!");
                        writer.Write("[SERVER] Upgrade your client to version " + SERVER_VERSION);
                        client.GetStream().Close();
                        client.Close();
                        continue;
                    }

                    String name = reader.ReadString();
                    String lang = reader.ReadString();
                    String connectionTime = DateTime.Now.ToString("h:mm:ss tt");

                    Console.WriteLine("Client connected: " + name + " from IP: [" + IP.ToString() + "] at " + connectionTime);
                    
                    if (names.Contains(name))
                    {
                        Console.WriteLine("Client disconnected: This name is already taken!");
                        writer.Write(langDict[lang]["NAME_TAKEN"]);
                        client.GetStream().Close();
                        client.Close();
                    }
                    else
                    {
                        writer.Write(langDict[lang]["WELCOME"] + name + "!");
                        names.Add(name);
                        BroadcastServerMessage(name, "JOINED_CHAT");
                        users.Add(new Client(ref client, ref reader, ref name, connectionTime, lang));
                    }
                }
            });
            waitForConnect.Start();
        }

        public static void Broadcast(String message, String senderName)
        {
            foreach (Client client in users)
            {
                try
                {
                    client.writer.Write("[" + senderName + "] " + message);
                    client.writer.Flush();
                }
                catch { }
            }
        }

        public static void BroadcastServerMessage(String messageCode)
        {
            foreach (Client client in users)
            {
                try
                {
                    client.writer.Write(langDict[client.lang]["PREFIX"] + langDict[client.lang][messageCode]);
                    client.writer.Flush();
                }
                catch { }
            }
        }

        public static void BroadcastServerMessage(String name, String messageCode)
        {
            if(users.Count > 0)
            {
                foreach (Client client in users)
                {
                    try
                    {
                        client.writer.Write(name + langDict[client.lang][messageCode]);
                        client.writer.Flush();
                    }
                    catch { }
                }
            }
        }

        public static void BroadcastImage(byte[] byteImage, String senderName)
        {
            foreach (Client client in users)
            {
                try
                {
                    Console.WriteLine(client.name);
                    client.writer.Write(senderName + langDict[client.lang]["IMAGE_SENT"]);
                    client.writer.Flush();
                    client.writer.Write("/img");
                    client.writer.Write(senderName);
                    client.writer.Write(byteImage.Length);
                    client.writer.Write(byteImage);
                }
                catch { }
            }
        }

        public static void Delete(Client client)
        {
            Console.WriteLine("Client disconnected: " + client.name + " at " + DateTime.Now.ToString("h:mm:ss tt"));
            BroadcastServerMessage(client.name, "LEFT_CHAT");
            names.Remove(client.name);
            users.Remove(client);
            client.message.Abort();
        }

        private static void InitializeLang()
        {
            langDict.Add("pl_PL", new Dictionary<string, string>()
            {
                {"PREFIX", "[SERWER] "},
                {"NAME_TAKEN", "[SERWER] Ta nazwa użytkownika jest zajęta!"},
                {"LEFT_CHAT", " wyszedł z czatu"},
                {"JOINED_CHAT", " dołączył do czatu"},
                {"IMAGE_SENT", " wysłał zdjęcie"},
                {"WELCOME", "[SERWER] Witamy na czacie, "},
            });
            langDict.Add("en_US", new Dictionary<string, string>()
            {
                {"PREFIX", "[SERVER] "},
                {"NAME_TAKEN", "[SERVER] This name is already taken!"},
                {"LEFT_CHAT", " left the chat"},
                {"JOINED_CHAT", " joined the chat"},
                {"IMAGE_SENT", " sent an image"},
                {"WELCOME", "[SERVER] Welcome on the chat, "},
            });
        }
    }
}
