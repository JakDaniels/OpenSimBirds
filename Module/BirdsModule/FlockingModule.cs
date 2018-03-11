/*
 * Copyright (c) Contributors, https://github.com/jonc/osboids
 * https://github.com/JakDaniels/OpenSimBirds
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
using System;
using System.Timers;
using System.Net;
using System.IO;
using System.Text;
using System.Collections.Generic;
using OpenMetaverse;
using Nini.Config;
using System.Threading;
using log4net;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using Mono.Addins;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;

[assembly: Addin("OpenSimBirds", "0.2")]
[assembly: AddinDependency("OpenSim.Region.Framework", OpenSim.VersionInfo.VersionNumber)]

namespace Flocking
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "OpenSimBirds")]
    public class FlockingModule : INonSharedRegionModule
    {
        #region Fields
        private static readonly ILog m_log = LogManager.GetLogger (System.Reflection.MethodBase.GetCurrentMethod ().DeclaringType);

        public string m_name = "OpenSimBirds";
        private string m_regionConfigDir = "";
        private Scene m_scene;
        private ICommandConsole m_console;
        private FlockingModel m_model;
        private FlockingView m_view;
        private bool m_startup = true;
        private bool m_enabled = false;
        private bool m_ready = false;
        private uint m_frame = 0;
        private int m_frameUpdateRate = 1;
        private int m_chatChannel = 118;
        private string m_birdPrim;
        private int m_flockSize = 50;
        private int m_maxFlockSize = 100;
        private float m_maxSpeed;
        private float m_maxForce;
        private float m_neighbourDistance;
        private float m_desiredSeparation;
        private float m_tolerance;
        private float m_borderSize;
        private int m_maxHeight;
        private Vector3 m_shoutPos = new Vector3(128f, 128f, 30f);
        static object m_sync = new object();
        private List<UUID> m_allowedControllers = new List<UUID>();

        public IConfigSource m_config;

        private UUID m_owner;
        #endregion

        #region IRegionModuleBase implementation

        public string Name { get { return m_name; } }
        public Type ReplaceableInterface { get { return null; } }
        public bool IsSharedModule { get { return false; } }

        public void Initialise (IConfigSource source)
        {
            m_config = source;
        }

        public void AddRegion (Scene scene)
        {
            IConfig cnf;
            m_log.InfoFormat("[{0}]: Adding region {1} to this module", m_name, scene.RegionInfo.RegionName);

            cnf = m_config.Configs["Startup"];
            m_regionConfigDir = cnf.GetString("regionload_regionsdir", Path.Combine(Util.configDir(), "bin/Regions/"));

            cnf = m_config.Configs[scene.RegionInfo.RegionName];

            if (cnf == null)
            {
                m_log.InfoFormat("[{0}]: No region section [{1}] found in addon-modules/{2}/config/*.ini configuration files.", m_name, scene.RegionInfo.RegionName, m_name);
                //string moduleConfigFile = Path.Combine(Util.configDir(),m_name + ".ini");
                string moduleConfigFile = Path.Combine(m_regionConfigDir, "Regions.ini");
                try
                {
                    m_log.InfoFormat("[{0}]: Checking {1} for [{2}] section containing valid config keys", m_name, moduleConfigFile, scene.RegionInfo.RegionName);
                    m_config = new IniConfigSource(moduleConfigFile);
                    cnf = m_config.Configs[scene.RegionInfo.RegionName];
                }
                catch (Exception)
                {
                    cnf = null;
                }

                if (cnf == null)
                {
                    m_log.InfoFormat("[{0}]: No region section [{1}] found in configuration {2}. Birds in this region are set to Disabled", m_name, scene.RegionInfo.RegionName, moduleConfigFile);
                    m_enabled = false;
                    return;
                }
            }

            m_startup = cnf.GetBoolean("BirdsModuleStartup", true);

            if (m_startup)
            {
                m_scene = scene;
                m_enabled = cnf.GetBoolean("BirdsEnabled", false);
                m_chatChannel = cnf.GetInt("BirdsChatChannel", 118);
                m_birdPrim = cnf.GetString("BirdsPrim", "birdPrim");
                m_flockSize = cnf.GetInt("BirdsFlockSize", 20);
                m_maxFlockSize = cnf.GetInt("BirdsMaxFlockSize", 100);
                m_maxSpeed = cnf.GetFloat("BirdsMaxSpeed", 1.5f);
                m_maxForce = cnf.GetFloat("BirdsMaxForce", 0.2f);
                m_neighbourDistance = cnf.GetFloat("BirdsNeighbourDistance", 25f);
                m_desiredSeparation = cnf.GetFloat("BirdsDesiredSeparation", 10f);
                m_tolerance = cnf.GetFloat("BirdsTolerance", 5f);
                m_borderSize = cnf.GetFloat("BirdsRegionBorderSize", 5f);
                m_maxHeight = cnf.GetInt("BirdsMaxHeight", 75);
                m_frameUpdateRate = cnf.GetInt("BirdsUpdateEveryNFrames", 1);

                string allowedControllers = cnf.GetString("BirdsAllowedControllers", UUID.Zero.ToString());
                if (allowedControllers != UUID.Zero.ToString())
                {
                    string[] ac = allowedControllers.Split(new char[] { ',' });
                    UUID acUUID;
                    for (int i = 0; i < ac.Length; i++)
                    {
                        string value = ac[i].Trim();
                        if (value == "ESTATE_OWNER")
                        {
                            UUID eoUUID = m_scene.RegionInfo.EstateSettings.EstateOwner;
                            m_allowedControllers.Add(eoUUID);
                            m_log.InfoFormat("[{0}] Added Estate Owner UUID: {1} to list of allowed users", m_name, eoUUID.ToString());
                            continue;
                        }
                        if (value == "ESTATE_MANAGER")
                        {
                            foreach (UUID emUUID in m_scene.RegionInfo.EstateSettings.EstateManagers)
                            {
                                m_allowedControllers.Add(emUUID);
                                m_log.InfoFormat("[{0}] Added Estate Manager UUID: {1} to list of allowed users", m_name, emUUID.ToString());
                            }
                            continue;
                        }
                        if (UUID.TryParse(ac[i].Trim(), out acUUID))
                        {
                            m_allowedControllers.Add(acUUID);
                            m_log.InfoFormat("[{0}] Added UUID: {1} to list of allowed users", m_name, acUUID.ToString());
                        }
                    }
                }
                else
                {
                    m_log.InfoFormat("[{0}] No command security was defined in the config. Any user may possibly configure this module from a script!", m_name);
                }

                m_log.InfoFormat("[{0}] Module is {1} listening for commands on channel {2} with Flock Size {3}", m_name, m_enabled ? "enabled and" : "disabled, but still", m_chatChannel, m_flockSize);

                m_console = MainConsole.Instance;
                //register commands with the scene
                RegisterCommands();

                //register handlers
                m_scene.EventManager.OnFrame += FlockUpdate;
                m_scene.EventManager.OnChatFromClient += SimChatSent; //listen for commands sent from the client
                m_scene.EventManager.OnChatFromWorld += SimChatSent;
                m_scene.EventManager.OnPrimsLoaded += PrimsLoaded;

                // init module
                m_model = new FlockingModel(m_name, m_maxSpeed, m_maxForce, m_neighbourDistance, m_desiredSeparation, m_tolerance, m_borderSize);
                m_view = new FlockingView(m_name, m_scene);
                m_view.BirdPrim = m_birdPrim;
                m_frame = 0;
                m_shoutPos = new Vector3(scene.RegionInfo.RegionSizeX / 2f, scene.RegionInfo.RegionSizeY / 2f, 30f);

                FlockInitialise();
            }
            else m_log.InfoFormat("[{0}] Module is disabled in Region {1}", m_name, scene.RegionInfo.RegionName);
        }

        public void RegionLoaded (Scene scene)
        {
            if (m_startup)
            {
                // Mark Module Ready for duty
                m_ready = true;
            }
        }

        public void PrimsLoaded(Scene scene)
        {
            m_scene = scene;
            ClearPersisted();
        }

        public void RemoveRegion (Scene scene)
        {
            m_log.InfoFormat("[{0}]: Removing region {1} from this module", m_name, scene.RegionInfo.RegionName);
            if (m_startup) {
                m_view.Clear();
                scene.EventManager.OnFrame -= FlockUpdate;
                scene.EventManager.OnChatFromClient -= SimChatSent;
                scene.EventManager.OnChatFromWorld -= SimChatSent;
                scene.EventManager.OnPrimsLoaded -= PrimsLoaded;
                m_ready = false;
            }
        }

        public void Close()
        {
            if (m_startup)
            {
                m_scene.EventManager.OnFrame -= FlockUpdate;
                m_scene.EventManager.OnChatFromClient -= SimChatSent;
                m_scene.EventManager.OnChatFromWorld -= SimChatSent;
                m_scene.EventManager.OnPrimsLoaded -= PrimsLoaded;
                m_ready = false;
            }
        }

        #endregion

        #region Helpers

        public void FlockInitialise()
        {
            //make a flow map for this scene
            FlowMap flowMap = new FlowMap(m_scene, m_maxHeight, m_borderSize);
            flowMap.Initialise();

            // Generate initial flock values
            m_model.Initialise(m_flockSize, flowMap);

            // who is the owner for the flock in this region
            m_owner = m_scene.RegionInfo.EstateSettings.EstateOwner;
            m_view.PostInitialize(m_owner);
        }

        public void ClearPersisted()
        {
            //really trash all possible birds that may have been persisted at last shutdown
            int i;
            for (i = m_flockSize; i < m_maxFlockSize; i++)
            {
                foreach (EntityBase e in m_scene.GetEntities())
                {
                    if (e.Name == (string)(m_name + i))
                    {
                        m_log.InfoFormat("[{0}]: Removing old persisted prim {1} from region {2}", m_name, m_name + i, m_scene.RegionInfo.RegionName);
                        m_scene.DeleteSceneObject((SceneObjectGroup)e, false);
                        break;
                    }
                }
            }
            m_scene.ForceClientUpdate();
        }

        #endregion

        #region EventHandlers

        public void FlockUpdate ()
        {
            if (!m_ready || !m_enabled || ((m_frame++ % m_frameUpdateRate) != 0))
            {
                return;
            }

            //m_log.InfoFormat("update my birds");

            // work out where everyone has moved to
            // and tell the scene to render the new positions
            lock( m_sync ) {
                List<Bird > birds = m_model.UpdateFlockPos ();
                m_view.Render (birds);
            }
        }

        protected void SimChatSent (Object x, OSChatMessage msg)
        {
            if (msg.Channel != m_chatChannel) return; // not for us
            if (m_allowedControllers.Count > 0)
            {
                bool reject = true;
                if (msg.SenderObject != null)
                {
                    UUID ooUUID = ((SceneObjectPart)msg.SenderObject).OwnerID;
                    //m_log.InfoFormat("[{0}]: Message from object {1} with OwnerID: {2}", m_name, msg.SenderUUID, ooUUID);
                    if (m_allowedControllers.Contains(ooUUID)) reject = false;
                }
                if (m_allowedControllers.Contains(msg.SenderUUID)) reject = false;

                if (reject) return; //not for us
            }

            // try and parse a valid cmd from this msg
            string cmd = msg.Message; //.ToLower ();

            //stick ui in the args so we know to respond in world
            //bit of a hack - but lets us use CommandDelegate inWorld
            string[] args = (cmd + " <ui>").Split (" ".ToCharArray ());

            if (cmd.StartsWith ("stop")) {
                HandleStopCmd (m_name, args);
            } else if (cmd.StartsWith ("start")) {
                HandleStartCmd (m_name, args);
            } else if (cmd.StartsWith("enable")) {
                HandleEnableCmd(m_name, args);
            } else if (cmd.StartsWith("disable")) {
                HandleDisableCmd(m_name, args);
            } else if (cmd.StartsWith ("size")) {
                HandleSetSizeCmd (m_name, args);
            } else if (cmd.StartsWith ("stats")) {
                HandleShowStatsCmd (m_name, args);
            } else if (cmd.StartsWith ("prim")) {
                HandleSetPrimCmd (m_name, args);
            } else if (cmd.StartsWith("speed")) {
                HandleSetMaxSpeedCmd(m_name, args);
            } else if (cmd.StartsWith("force")) {
                HandleSetMaxForceCmd(m_name, args);
            } else if (cmd.StartsWith("distance")) {
                HandleSetNeighbourDistanceCmd(m_name, args);
            } else if (cmd.StartsWith("separation")) {
                HandleSetDesiredSeparationCmd(m_name, args);
            } else if (cmd.StartsWith("tolerance")) {
                HandleSetToleranceCmd(m_name, args);
            } else if (cmd.StartsWith ("framerate")) {
                HandleSetFrameRateCmd (m_name, args);
            }
        }

        #endregion

        #region Command Handling

        private void AddCommand (string cmd, string args, string help, CommandDelegate fn)
        {
            string argStr = "";
            if (args.Trim ().Length > 0) {
                argStr = " <" + args + "> ";
            }
            m_log.InfoFormat("[{0}]: Adding console command {1} - {2} to region {3}", m_name, "birds-" + cmd + argStr, help, m_scene.RegionInfo.RegionName);
            //m_scene.AddCommand (this, "birds-" + cmd, "birds-" + cmd + argStr, help, fn);
            m_console.Commands.AddCommand(m_name, false, "birds-" + cmd, "birds-" + cmd + argStr, help, fn);
        }

        private void RegisterCommands ()
        {
            AddCommand ("stop", "", "Stop Birds Flocking", HandleStopCmd);
            AddCommand ("start", "", "Start Birds Flocking", HandleStartCmd);
            AddCommand ("enable", "", "Enable Birds Flocking", HandleEnableCmd);
            AddCommand ("disable", "", "Disable Birds Flocking", HandleDisableCmd);
            AddCommand ("size", "num", "Adjust the size of the flock ", HandleSetSizeCmd);
            AddCommand ("prim", "name", "Set the prim used for each bird to that passed in", HandleSetPrimCmd);
            AddCommand ("speed", "num", "Set the maximum velocity each bird may achieve", HandleSetMaxSpeedCmd);
            AddCommand("force", "num", "Set the maximum force each bird may accelerate", HandleSetMaxForceCmd);
            AddCommand("distance", "num", "Set the maximum distance that other birds are to be considered in the same flock as us", HandleSetNeighbourDistanceCmd);
            AddCommand("separation", "num", "How far away from other birds we would like to stay", HandleSetDesiredSeparationCmd);
            AddCommand("tolerance", "num", "How close to the edges of things can we get without being worried", HandleSetToleranceCmd);
            AddCommand("stats", "", "Show birds stats", HandleShowStatsCmd);
            AddCommand("framerate", "num", "[debugging] only update birds every <num> frames", HandleSetFrameRateCmd);
        }

        private void RegisterScriptFunctions()
        {
            IScriptModuleComms comms = m_scene.RequestModuleInterface<IScriptModuleComms>();
            if (comms != null)
            {
                comms.RegisterScriptInvocation(this, "birdsGetStats");
            }
        }

        private string birdsGetStats(UUID host, UUID script, string stat)
        {
            return ""; //currently a placeholder
        }

        private bool ShouldHandleCmd ()
        {
            if (!(m_console.ConsoleScene == null || m_console.ConsoleScene == m_scene)) {
                return false;
            }
            return true;
        }

        private bool IsInWorldCmd (ref string [] args)
        {
            if (args.Length > 0 && args [args.Length - 1].Equals ("<ui>")) {
                m_log.InfoFormat("[{0}]: Inworld command detected in region {1}", m_name, m_scene.RegionInfo.RegionName);
                return true;
            }
            return false;
        }

        private void ShowResponse (string response, bool inWorld)
        {
            if (inWorld) {
                //IClientAPI ownerAPI = null;
                //if (m_scene.TryGetClient (m_owner, out ownerAPI)) {
                //  ownerAPI.SendBlueBoxMessage (m_owner, "Birds", response);
                //}
                SendSimChat(response, m_chatChannel);
            } else {
                MainConsole.Instance.Output (response);
            }
        }

        private void SendSimChat(string msg, int channel)
        {
            m_scene.SimChatBroadcast(Utils.StringToBytes(msg), ChatTypeEnum.Region, channel, m_shoutPos, m_name, UUID.Zero, false);
        }

        public void HandleDisableCmd(string module, string[] args)
        {
            if (m_ready && ShouldHandleCmd ()) {
                m_log.InfoFormat("[{0}]: Bird flocking is disabled in region {1}.", m_name, m_scene.RegionInfo.RegionName);
                m_enabled = false;
                //m_ready = false;
                m_view.Clear();
            }
        }

        public void HandleEnableCmd(string module, string[] args)
        {
            if (m_ready && ShouldHandleCmd())
            {
                m_log.InfoFormat("[{0}]: Bird flocking is enabled in region {1}.", m_name, m_scene.RegionInfo.RegionName);
                m_enabled = true;
                //m_ready = true;
            }
        }

        public void HandleStopCmd (string module, string[] args)
        {
            if (m_enabled && m_ready && ShouldHandleCmd())
            {
                m_log.InfoFormat("[{0}]: Bird flocking is stopped in region {1}.", m_name, m_scene.RegionInfo.RegionName);
                m_enabled = false;
            }
        }

        public void HandleStartCmd(string module, string[] args)
        {
            if (!m_enabled && m_ready && ShouldHandleCmd())
            {
                m_log.InfoFormat("[{0}]: Bird flocking is started in region {1}.", m_name, m_scene.RegionInfo.RegionName);
                m_enabled = true;
                FlockUpdate();
            }
        }

        void HandleSetFrameRateCmd (string module, string[] args)
        {
            if (m_ready && ShouldHandleCmd())
            {
                int frameRate = Convert.ToInt32( args[1] );
                m_frameUpdateRate = frameRate;
                m_log.InfoFormat("[{0}]: Bird updates set to every {1} frames in region {2}.", m_name, frameRate, m_scene.RegionInfo.RegionName);
            }
        }

        public void HandleSetSizeCmd (string module, string[] args)
        {
            if (m_ready && ShouldHandleCmd())
            {
                lock( m_sync ) {
                    int newSize = Convert.ToInt32(args[1]);
                    if (newSize > m_maxFlockSize) newSize = m_maxFlockSize;
                    m_view.Clear();
                    m_model.Size = newSize;
                    m_log.InfoFormat("[{0}]: Bird flock size is set to {1} in region {2}.", m_name, newSize, m_scene.RegionInfo.RegionName);
                }
            }
        }

        public void HandleShowStatsCmd (string module, string[] args)
        {
            if (m_ready && ShouldHandleCmd())
            {
                bool inWorld = IsInWorldCmd (ref args);
                int i;
                int s=m_model.Size;
                UUID primUuid;
                List<ScenePresence> avatarsSitList = new List<ScenePresence>();
                string avUuids;
                if (inWorld)
                {
                    m_log.InfoFormat("[{0}]: Sending bird statistics to region {1}.", m_name, m_scene.RegionInfo.RegionName);
                }

                ShowResponse("birds-started = " + (m_enabled ? "True" : "False"), inWorld);
                ShowResponse("birds-enabled = " + (m_ready ? "True" : "False"), inWorld);
                ShowResponse("birds-prim = " + m_view.BirdPrim, inWorld);
                ShowResponse("birds-framerate = " + m_frameUpdateRate, inWorld);
                ShowResponse("birds-maxsize = " + m_maxFlockSize, inWorld);
                ShowResponse("birds-size = " + s, inWorld);
                ShowResponse("birds-speed = " + m_model.MaxSpeed, inWorld);
                ShowResponse("birds-force = " + m_model.MaxForce, inWorld);
                ShowResponse("birds-distance = " + m_model.NeighbourDistance, inWorld);
                ShowResponse("birds-separation = " + m_model.DesiredSeparation, inWorld);
                ShowResponse("birds-tolerance = " + m_model.Tolerance, inWorld);
                ShowResponse("birds-border = " + m_borderSize, inWorld);

                for (i = 0; i < s; i++)
                {
                    primUuid = UUID.Zero;
                    avatarsSitList.Clear();
                    avUuids = "";
                    foreach (EntityBase e in m_scene.GetEntities())
                    {
                        if (e.Name == m_name + i)
                        {
                            SceneObjectGroup sog = (SceneObjectGroup)e;
                            SceneObjectPart rootPart = sog.RootPart;
                            primUuid = rootPart.UUID;
                            avatarsSitList = sog.GetSittingAvatars();
                            foreach (ScenePresence av in avatarsSitList)
                            {
                                avUuids += av.UUID.ToString() + " ";
                            }

                            break;
                        }
                    }

                    ShowResponse("birds-prim" + i + " = " + m_name + i + " : " + primUuid.ToString() + " : " + avUuids.Trim(), inWorld);
                }
            }
        }

        public void HandleSetPrimCmd (string module, string[] args)
        {
            if (m_ready && ShouldHandleCmd())
            {
                string primName = args[1];
                lock(m_sync) {
                    m_view.BirdPrim = primName;
                    m_log.InfoFormat("[{0}]: Bird prim is set to {1} in region {2}.", m_name, primName, m_scene.RegionInfo.RegionName);
                    m_view.Clear();
                }
            }
        }

        public void HandleSetMaxSpeedCmd(string module, string[] args)
        {
            if (m_ready && ShouldHandleCmd())
            {
                float maxSpeed = (float)Convert.ToDecimal(args[1]);
                lock (m_sync)
                {
                    m_model.MaxSpeed = maxSpeed;
                    m_log.InfoFormat("[{0}]: Birds maximum speed is set to {1} in region {2}.", m_name, maxSpeed, m_scene.RegionInfo.RegionName);
                }
            }
        }

        public void HandleSetMaxForceCmd(string module, string[] args)
        {
            if (m_ready && ShouldHandleCmd())
            {
                float maxForce = (float)Convert.ToDecimal(args[1]);
                lock (m_sync)
                {
                    m_model.MaxForce = maxForce;
                    m_log.InfoFormat("[{0}]: Birds maximum force is set to {1} in region {2}.", m_name, maxForce, m_scene.RegionInfo.RegionName);
                }
            }
        }

        public void HandleSetNeighbourDistanceCmd(string module, string[] args)
        {
            if (m_ready && ShouldHandleCmd())
            {
                float neighbourDistance = (float)Convert.ToDecimal(args[1]);
                lock (m_sync)
                {
                    m_model.NeighbourDistance = neighbourDistance;
                    m_log.InfoFormat("[{0}]: Birds neighbour distance is set to {1} in region {2}.", m_name, neighbourDistance, m_scene.RegionInfo.RegionName);
                }
            }
        }

        public void HandleSetDesiredSeparationCmd(string module, string[] args)
        {
            if (m_ready && ShouldHandleCmd())
            {
                float desiredSeparation = (float)Convert.ToDecimal(args[1]);
                lock (m_sync)
                {
                    m_model.DesiredSeparation = desiredSeparation;
                    m_log.InfoFormat("[{0}]: Birds desired separation is set to {1} in region {2}.", m_name, desiredSeparation, m_scene.RegionInfo.RegionName);
                }
            }
        }

        public void HandleSetToleranceCmd(string module, string[] args)
        {
            if (m_ready && ShouldHandleCmd())
            {
                float tolerance = (float)Convert.ToDecimal(args[1]);
                lock (m_sync)
                {
                    m_model.Tolerance = tolerance;
                    m_log.InfoFormat("[{0}]: Birds tolerance is set to {1} in region {2}.", m_name, tolerance, m_scene.RegionInfo.RegionName);
                }
            }
        }

        #endregion
    }
}
