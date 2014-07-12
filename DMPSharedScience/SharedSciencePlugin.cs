using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DarkMultiPlayerCommon;
using DarkMultiPlayerServer;
using MessageStream;

namespace DMPSharedScience
{
    public class SharedSciencePlugin : DMPPlugin
    {
        #region Fields

        private const string RDScenarioName = "ResearchAndDevelopment";
        private const string ScenarioFolderName = "Scenarios";
        private const string InitialUserFolderName = "Initial";

        private const string CONFIG_FILENAME = "SharedScenarios.cfg";

        HashSet<string> sharedScenarioSet = new HashSet<string>();

        #endregion

        #region Constructors

        public SharedSciencePlugin()
        {

        }

        #endregion

        #region Plugin Methods

        public override void OnServerStart()
        {
            Log("Started.");
            
            LoadSharedScenarios();
            CommandHandler.RegisterCommand("scenario", ProcessSharedScenarioCommand, "Managed shared scenarios");
        }

        private void ProcessSharedScenarioCommand(string commandPart)
        {
            string argumentPart = "";

            int firstSpaceIndex = commandPart.IndexOf(' ');
            if (firstSpaceIndex > -1)
            {
                if (commandPart.Length > firstSpaceIndex + 1)
                {
                    argumentPart = commandPart.Substring(firstSpaceIndex + 1);
                }
                commandPart = commandPart.Substring(0, firstSpaceIndex);
            }

            argumentPart = argumentPart.ToLower();

            switch (commandPart)
            {
                case "share":
                    if (sharedScenarioSet.Add(argumentPart))
                    {
                        string msg = "Added shared scenario: " + argumentPart;
                        Log(msg);
                        ClientHandler.SendChatMessageToAll(msg);
                    }
                    else
                    {
                        string msg = "Cannot share, scenario is already shared: " + argumentPart;
                        Log(msg);
                    }
                    SaveSharedScenarios();
                    break;
                case "unshare":
                    if (sharedScenarioSet.Remove(argumentPart))
                    {
                        string msg = "Unshared scenario: " + argumentPart;
                        Log(msg);
                        ClientHandler.SendChatMessageToAll(msg);
                    }
                    else
                    {
                        string msg = "Cannot unshare, scenario was not being shared: " + argumentPart;
                        Log(msg);
                    }
                    SaveSharedScenarios();
                    break;
                case "list":
                default:
                    string msg2 = "Currently sharing scenarios: ";
                    foreach (var scenario in sharedScenarioSet)
                    {
                        msg2 += scenario + " ";
                    }
                    Log(msg2);
                    break;
            }
        }

        private void LoadSharedScenarios()
        {
            string path = Path.Combine(AssemblyDirectory, CONFIG_FILENAME);

            if (File.Exists(path))
            {
                Log("Loading shared scenarios from " + CONFIG_FILENAME);
                
                string[] lines = File.ReadAllLines(path);
                sharedScenarioSet.Clear();

                foreach (var line in lines)
                {
                    sharedScenarioSet.Add(line.ToLower());
                }
            }
            else
            {
                File.WriteAllText(path, "");
            }
        }

        private void SaveSharedScenarios()
        {
            try
            {
                Log("Saving shared scenarios to " + CONFIG_FILENAME);
                string[] lines = sharedScenarioSet.ToArray();

                string path = Path.Combine(AssemblyDirectory, CONFIG_FILENAME);
                File.WriteAllLines(path, lines);
            }
            catch (Exception ex)
            {
                Log("Exception saving shared scenarios to " + CONFIG_FILENAME + ": " + ex.ToString());
            }
        }

        public override void OnMessageReceived(ClientObject client, ClientMessage message)
        {
            if (!client.authenticated)
            {
                //Only handle authenticated messages
                return;
            }

            if (message.type == ClientMessageType.SCENARIO_DATA)
            {
                HandleScenarioMessage(client, message);
                message.handled = true;
            }
        }

        #endregion

        #region Private Methods

        private void HandleScenarioMessage(ClientObject client, ClientMessage message)
        {
            using (MessageReader mr = new MessageReader(message.data, false))
            {
                string[] scenarioName = mr.Read<string[]>();
                string[] scenarioData = mr.Read<string[]>();

                for (int i = 0; i < scenarioName.Length; i++)
                {
                    string scenarioNodeName = scenarioName[i];

                    if (sharedScenarioSet.Contains(scenarioNodeName.ToLower()))
                    {
                        Log("Syncing scenario module " + scenarioNodeName + " from " + client.playerName);

                        SaveScenarioToInitialScenarioFolder(scenarioNodeName, scenarioData[i]);
                        CopyScenarioFromInitialToAllUsers(scenarioNodeName);
                        SendScenarioToOtherClients(scenarioNodeName, client);
                    }
                }
            }
        }

        private void SaveScenarioToInitialScenarioFolder(string scenarioNodeName, string data)
        {
            string initialFilepath = GetInitialScenarioFilePath(scenarioNodeName);

            File.WriteAllText(initialFilepath, data);
        }

        private void CopyScenarioFromInitialToAllUsers(string scenarioNodeName)
        {
            string initialFilePath = GetInitialScenarioFilePath(scenarioNodeName);

            if (!File.Exists(initialFilePath))
            {
                return;
            }

            string initialDirectory = Path.GetDirectoryName(initialFilePath);

            string filename = scenarioNodeName + ".txt";
            string scenarioPath = Path.Combine(Server.universeDirectory, ScenarioFolderName);

            var userDirectories = Directory.EnumerateDirectories(scenarioPath);
            foreach (var userDirectory in userDirectories)
            {
                if (userDirectory != initialDirectory)
                {
                    string userFilePath = Path.Combine(userDirectory, filename);
                    File.Copy(initialFilePath, userFilePath, true);
                }
            }
        }

        private void SendScenarioToOtherClients(string scenarioNodeName, ClientObject fromClient)
        {
            string[] scenarioNameArray = new string[1];
            string[] scenarioDataArray = new string[1];

            string initialFilePath = GetInitialScenarioFilePath(scenarioNodeName);
            string scenarioData = File.ReadAllText(initialFilePath);

            scenarioNameArray[0] = scenarioNodeName;
            scenarioDataArray[0] = scenarioData;

            ServerMessage newMessage = new ServerMessage();
            newMessage.type = ServerMessageType.SCENARIO_DATA;
            using (MessageWriter mw = new MessageWriter())
            {
                mw.Write<string[]>(scenarioNameArray);
                mw.Write<string[]>(scenarioDataArray);
                newMessage.data = mw.GetMessageBytes();
            }

            ClientHandler.SendToAll(fromClient, newMessage, false);
        }

        private string GetInitialScenarioFilePath(string scenarioNodeName)
        {
            string filename = scenarioNodeName + ".txt";

            string initialFilePath = Path.Combine(Server.universeDirectory, ScenarioFolderName, InitialUserFolderName, filename);

            return initialFilePath;
        }

        private void Log(string message)
        {
            DarkLog.Debug("[SharedSciencePlugin] " + message);
        }
        static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        #endregion
    }
}