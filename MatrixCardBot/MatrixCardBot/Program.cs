﻿using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using MatrixSDK.Client;
using MatrixSDK.Structures;
namespace MatrixCardBot
{
	class Program
	{
		public static MatrixClient Client;
		public static Dictionary<BotCmd,MethodInfo> Cmds = new Dictionary<BotCmd, MethodInfo>();

		public static LobbyManager LobbyManager;

		public static void Main (string[] args)
		{
			Console.WriteLine ("Reading INI File");
			string cfgpath;

			if (args.Length > 1) {
				cfgpath = args [1];
			} else {
				cfgpath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.config/cardbot.ini";
			}
			cfgpath = System.IO.Path.GetFullPath (cfgpath);
			Console.WriteLine("Trying to read from " + cfgpath);
			Configuration.ReadConfig (cfgpath);

			Console.WriteLine ("Connecting to Matrix");
			Client = new MatrixClient (Configuration.Config ["matrix"] ["host"]);
			
			Console.WriteLine("Connected. Logging in");
			Client.LoginWithPassword (Configuration.Config ["matrix"] ["user"], Configuration.Config ["matrix"] ["pass"]);
			Console.WriteLine("Logged in OK");
			Console.WriteLine("Joining Rooms:");
			foreach (string roomid in Configuration.Config ["matrix"] ["rooms"].Split(',')) {
				MatrixRoom room = Client.GetRoomByAlias (roomid);
				if (room == null) {
					room = Client.JoinRoom (roomid);
					if (room != null) {
						Console.WriteLine ("\tJoined " + roomid);
						room.OnMessage += Room_OnMessage;
					} else {
						Console.WriteLine ("\tCouldn't find " + roomid);
					}
				} else {
					room.OnMessage += Room_OnMessage;
				}
			}
			Console.WriteLine ("Done!");
			//Find commands
			foreach(MethodInfo method in typeof(Commands).GetMethods(BindingFlags.Static|BindingFlags.Public)){
				BotCmd cmd = method.GetCustomAttribute<BotCmd> ();
				if (cmd != null) {
					Cmds.Add (cmd, method);
				}
			}

            LobbyManager = new LobbyManager(Client);
            Console.CancelKeyPress +=  (object sender, ConsoleCancelEventArgs e) => {
                LobbyManager.Dispose();
                Client.Dispose();
            };

		}

		static void Room_OnMessage (MatrixRoom room, MatrixSDK.Structures.MatrixEvent evt)
		{
			if (evt.age > 3000) {
				return; // Too old
			}

			string msg = ((MatrixMRoomMessage)evt.content).body;

			if (msg.StartsWith ("!cardbot")) {
				msg = msg.Substring (9);
				string[] parts = msg.Split (' ');
				string cmd = parts [0].ToLower ();
				try
				{
					MethodInfo method = Cmds.First(x => { 
						return (x.Key.CMD == cmd) || ( x.Key.BeginsWith.Any( y => cmd.StartsWith(y) ));
					}).Value; 

					Task task = new Task (() => {
						method.Invoke (null, new object[3]{ msg, evt.sender, room });
					});
					task.Start ();	
				}
				catch(InvalidOperationException){
					//Command not found
				}
				catch(Exception e){
					Console.Error.WriteLine ("Problem with one of the commands");
					Console.Error.WriteLine (e);
				}
			}
            else if(msg == "botsnack"){
                room.SendMessage("Nom nom nom");
            }
		}
	}
}
