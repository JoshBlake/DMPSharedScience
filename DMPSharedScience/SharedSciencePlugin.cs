using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            CreateDefaultScenarioModuleIfMissing(RDScenarioName);
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

                    Log("Syncing scenario module " + scenarioNodeName + " from " + client.playerName);

                    SaveScenarioToInitialScenarioFolder(scenarioNodeName, scenarioData[i]);
                    CopyScenarioFromInitialToAllUsers(scenarioNodeName);
                    SendScenarioToOtherClients(scenarioNodeName, client);
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
            CreateDefaultScenarioModuleIfMissing(scenarioNodeName);

            string initialFilePath = GetInitialScenarioFilePath(scenarioNodeName);
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

        private void CreateDefaultScenarioModuleIfMissing(string scenarioNodeName)
        {
            string initialFilePath = GetInitialScenarioFilePath(scenarioNodeName);

            if (!File.Exists(initialFilePath))
            {
                Log("Creating default initial scenario for " + scenarioNodeName);
                File.WriteAllText(initialFilePath, GetDefaultRDFile());
            }
        }

        private string GetInitialScenarioFilePath(string scenarioNodeName)
        {
            string filename = scenarioNodeName + ".txt";

            string initialFilePath = Path.Combine(Server.universeDirectory, ScenarioFolderName, InitialUserFolderName, filename);

            return initialFilePath;
        }

        private string GetDefaultRDFile()
        {
            string ret = @"name = ResearchAndDevelopment
scene = 5, 6, 7, 8, 9
sci = 0
Tech
{
	id = start
	state = Available
	part = mk1pod
	part = liquidEngine
	part = solidBooster
	part = fuelTankSmall
	part = trussPiece1x
	part = longAntenna
	part = parachuteSingle
}
";

            return ret;
        }

        private void Log(string message)
        {
            DarkLog.Debug("[SharedSciencePlugin] " + message);
        }

        #endregion
    }
}