using System;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Text;
using System.Linq;

public class HelloWorld
{
  static public void Main ()
  {
    // Choose port
    Console.WriteLine("Please enter port number of the server (default: 8888):");
    string portInput = Console.ReadLine();
    int port = int.TryParse(portInput, out port) ? port : 8888;


    // Connect to server
    Console.WriteLine($"Attempting to connect to port {port}...");
    TcpClient client;
    try {
      client = new TcpClient("127.0.0.1", port);
    }
    catch (SocketException exception) {
      string error = exception.Message;
      Console.WriteLine($"Could not connect to server at {port} ({error})");
      return;
    }


    // Get a username
    string response;
    string username;
    NetworkStream stream = client.GetStream();
    Console.Clear();
    Console.WriteLine("Welcome to the chat... Please enter a username:");
    do {
      // Choose username
      username = Console.ReadLine().Trim();

      // Validate username
      if (String.IsNullOrEmpty(username)) {
        continue;
      }

      // Send username
      byte[] message = Encoding.Unicode.GetBytes(username);
      try {
        stream.Write(message, 0, message.Length);
      }
      catch (SocketException exception) {
        string error = exception.Message;
        Console.WriteLine($"Could not send username to server ({error})");
        return;
      }

      // Was it available?
      byte[] buffer = new byte[client.ReceiveBufferSize];
      int data = stream.Read(buffer, 0, client.ReceiveBufferSize);
      response = Encoding.Unicode.GetString(buffer, 0, data);

      if (response != "ACCEPT") {
        Console.WriteLine("Username denied... Max 10 chars. Must be unqiue. Try again:");
      }

    } while(response != "ACCEPT");




    // Chat loop
    List<KeyValuePair<string, string>> history = new List<KeyValuePair<string, string>>();
    string text = String.Empty;
    bool redrawUI = true;
    while (true) {

      // No need to iterate to quickly
      Thread.Sleep(10);


      // Limit history to screen size
      int overflow = history.Count - Console.WindowHeight + 6;
      if (overflow > 0) {
        history.Reverse();
        history.RemoveRange(0, overflow);
        history.Reverse();
      }


      // Print user input
      if (redrawUI) {
        Console.Clear();
        Console.WriteLine("TYPE IN YOUR MESSAGE AND HIT ENTER: ");
        Console.WriteLine("====================================");
        Console.Write("> ");
        Console.WriteLine(text);

        // Print chat history
        Console.WriteLine("====================================");
        foreach(var entry in history) {
          string paddedSender = ($"{entry.Key} ").PadRight(13, ' ');
          Console.WriteLine($"{paddedSender}{entry.Value}");
        }
      }

      // Check if user has pressed key
      if (Console.KeyAvailable) {
        ConsoleKeyInfo key = Console.ReadKey(true);
        redrawUI = true;

        // Choose action
        switch (key.Key) {

          // Delete character
          case ConsoleKey.Backspace:
            if (text.Length > 0)
              text = text.Substring(0, text.Length - 1);
            break;


            // Send message
          case ConsoleKey.Enter:
            history.Insert(0, new KeyValuePair<string, string>("You", text));
            string message = $"{username}:{text}";
            byte[] payload = Encoding.Unicode.GetBytes(message);
            try {
              stream.Write(payload, 0, payload.Length);
            }
            catch (SocketException exception) {
              string error = exception.Message;
              Console.WriteLine($"Could not send message to server ({error})");
              return;
            }
            text = String.Empty;
            break;


            // Add character to user input string
          default:
            text += key.KeyChar;
            break;
        }
      } else {
        redrawUI = false;
      }



      // Receive new messages
      if (stream.DataAvailable) {
        redrawUI = true;

        // Get message
        byte[] buffer = new byte[client.ReceiveBufferSize];
        int data = stream.Read(buffer, 0, client.ReceiveBufferSize);
        string receivedLine = Encoding.Unicode.GetString(buffer, 0, data);

        List<string> parts = receivedLine.Split(':').ToList();
        string receivedUsername = parts[0];
        parts.RemoveAt(0);
        string receivedText = String.Join(":", parts).Trim();

        // Add to history
        history.Insert(0, new KeyValuePair<string, string>(
              receivedUsername, receivedText));
      }
    }
  }
}
