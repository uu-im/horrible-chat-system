using System;
using System.Net;
using System.IO;
using System.Threading;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Text;
using System.Linq;

public class Server
{
  static public void Main ()
  {
    IDictionary<string, TcpClient> clients = new Dictionary<string, TcpClient>();



    // Choose port
    Console.WriteLine("Please enter port number to start server at (default: 8888)");
    string portInput = Console.ReadLine();
    int port = int.TryParse(portInput, out port) ? port : 8888;



    // Create and start server
    IPAddress ip = IPAddress.Parse("127.0.0.1");
    TcpListener server = new TcpListener(ip, port);
    try {
      server.Start();
      Console.WriteLine(String.Format("[Server]: Started at port {0}", port));
    }
    catch (SocketException exception) {
      string error = exception.Message;
      Console.WriteLine(String.Format("Could not start server: {0}", error));
      return;
    }



    // Chat loop
    while (true) {

      // Some delay is ok
      Thread.Sleep(100);



      // Check if we have pending connections
      if (server.Pending()) {

        // Connect to the client
        TcpClient client = server.AcceptTcpClient();
        Console.WriteLine("[Handshake]: New client connected");


        // Settle username with client
        string response;
        do {
          // Receive name from client
          string username;
          NetworkStream stream;
          try {
            stream = client.GetStream();
            byte[] buffer = new byte[client.ReceiveBufferSize];
            int data = stream.Read(buffer, 0, client.ReceiveBufferSize);
            username = Encoding.Unicode.GetString(buffer, 0, data);
            Console.WriteLine(String.Format("[Handshake]: Requesting username '{0}' ", username));
          }
          catch (IOException exception) {
            string error = exception.Message;
            Console.WriteLine(String.Format("[Handshake]: Did not receive username from new conncetion ({0})", error));
            break;
          }

          // Validate username
          bool isOk = String.IsNullOrEmpty(username) ? false : true;
          // Uniqueness
          foreach (var user in clients) {
            if (user.Key == username) {
              isOk = false;
            }
          }
          // Length
          if (username.Length > 10) {
            isOk = false;
          }

          if (isOk) {
            Console.WriteLine(String.Format("[Handshake]: Granting username '{0}' ", username));
            clients.Add(username, client);

            // Print all current users
            Console.WriteLine("===MEMBERS===");
            foreach(var c in clients) {
              Console.WriteLine(c.Key);
            }
            Console.WriteLine("=============");
          } else {
            Console.WriteLine(String.Format("[Handshake]: Rejecting username '{0}' ", username));
          }

          // Send username response
          try {
            response = isOk ? "ACCEPT" : "REJECT";
            byte[] message = Encoding.Unicode.GetBytes(response);
            stream.Write(message, 0, message.Length);
          } catch (IOException exception) {
            string error = exception.Message;
            Console.WriteLine(String.Format("[Handshake]: Could not send message back to client that requested {0} ({1})", username, error));
            break;
          }

        } while (response != "ACCEPT");
      }


      // Remove disconnected clients
      bool hadStaleClients = clients.Any(c => !c.Value.Connected);
      foreach(var client in clients.Where(c => !c.Value.Connected).ToList()) {
        Console.WriteLine(String.Format("[Remove]: [{0}]", client.Key));
      }
      clients = clients.Where(c => c.Value.Connected)
        .ToDictionary(c => c.Key, c => c.Value);
      if (hadStaleClients) {
        Console.WriteLine("===MEMBERS===");
        foreach(var c in clients) {
          Console.WriteLine(c.Key);
        }
        Console.WriteLine("=============");
      }


      // Check if new messages have arrived
      foreach(var sender in clients) {
        string senderName = sender.Key;
        TcpClient senderClient = sender.Value;
        NetworkStream senderStream = senderClient.GetStream();

        // Is there a new message?
        if (senderStream.DataAvailable) {
          string text;

          try {
            // Read the message
            byte[] buffer = new byte[senderClient.ReceiveBufferSize];
            int data = senderStream.Read(buffer, 0, senderClient.ReceiveBufferSize);
            text = Encoding.Unicode.GetString(buffer, 0, data);
          }
          catch (SocketException exception) {
            string error = exception.Message;
            Console.WriteLine(String.Format("[ERROR]: Could not read message from [{0}]", senderName));
            continue;
          }

          // Display message
          Console.WriteLine(String.Format("[Broadcast]: {0}", text));

          // Broadcast to all other clients
          foreach(var receiver in clients) {
            string receiverName = receiver.Key;
            if (receiverName != senderName) {
              TcpClient receiverClient = receiver.Value;
              Console.WriteLine(String.Format("[Send] from [{0}] to [{1}]", senderName, receiverName));
              try {
                NetworkStream receiverStream = receiverClient.GetStream();
                byte[] message = Encoding.Unicode.GetBytes(text);
                receiverStream.Write(message, 0, message.Length);
              } catch (IOException exception) {
                string error = exception.Message;
                Console.WriteLine(String.Format("[ERROR]: Broadcast from [{0}] to [{1}] failed.", senderName, receiverName));
              }
            }
          }

        }
      }






      // Close connection to client
      //client.Close();
    }

  }
}
