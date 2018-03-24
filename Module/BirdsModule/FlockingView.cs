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
using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using log4net;

namespace Flocking
{
    public class FlockingView
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private Scene m_scene;
        private UUID m_owner;
        private String m_name;
        private String m_birdPrim;

        private Dictionary<string, SceneObjectGroup> m_sogMap = new Dictionary<string, SceneObjectGroup> ();

        public FlockingView (String moduleName, Scene scene)
        {
            m_name = moduleName;
            m_scene = scene;
        }

        public void PostInitialize (UUID owner)
        {
            m_owner = owner;
        }

        public String BirdPrim {
            get { return m_birdPrim; }
            set{ m_birdPrim = value;}
        }

        public void Clear ()
        {
            //trash everything we have
            foreach (string name in m_sogMap.Keys)
            {
                m_log.InfoFormat("[{0}]: Removing prim {1} from region {2}", m_name, name, m_scene.RegionInfo.RegionName);
                SceneObjectGroup sog = m_sogMap[name];
                m_scene.DeleteSceneObject(sog, false);
            }
            m_sogMap.Clear();
            m_scene.ForceClientUpdate();
        }

        public void Render(List<Bird> birds)
        {
            foreach (Bird bird in birds) {
                DrawBird (bird);
            }
        }

        private void DrawBird (Bird bird)
        {
            SceneObjectPart existing = m_scene.GetSceneObjectPart (bird.Id);


            SceneObjectGroup sog;
            SceneObjectPart rootPart;

            if (existing == null) {
                m_log.InfoFormat("[{0}]: Adding prim {1} in region {2}", m_name, bird.Id, m_scene.RegionInfo.RegionName);
                SceneObjectGroup group = findByName (m_birdPrim);
                sog = CopyPrim (group, bird.Id);
                rootPart = sog.RootPart;
                rootPart.AddFlag(PrimFlags.Temporary);
                rootPart.AddFlag(PrimFlags.Phantom);
                //set prim to phantom
                //sog.UpdatePrimFlags(rootPart.LocalId, false, false, true, false);
                m_sogMap [bird.Id] = sog;
                m_scene.AddNewSceneObject (sog, false);
                // Fire script on_rez
                sog.CreateScriptInstances(0, true, m_scene.DefaultScriptEngine, 1);
                rootPart.ParentGroup.ResumeScripts();
                rootPart.ScheduleFullUpdate();
                sog.DetachFromBackup();
            } else {
                sog = existing.ParentGroup;
                m_sogMap[bird.Id] = sog;
                //rootPart = sog.RootPart;
                //set prim to phantom
                //sog.UpdatePrimFlags(rootPart.LocalId, false, false, true, false);
            }

            Quaternion rotation = CalcRotationToEndpoint (sog, sog.AbsolutePosition, bird.Location);
            sog.UpdateGroupRotationPR( bird.Location, rotation);
        }

        private static Quaternion CalcRotationToEndpoint (SceneObjectGroup copy, Vector3 sv, Vector3 ev)
        {
            //llSetRot(llRotBetween(<1,0,0>,llVecNorm(targetPosition - llGetPos())));
            // bird wil fly x forwards and Z up

            Vector3 currDirVec = Vector3.UnitX;
            Vector3 desiredDirVec = Vector3.Subtract (ev, sv);
            desiredDirVec.Normalize ();

            Quaternion rot = Vector3.RotationBetween (currDirVec, desiredDirVec);
            return rot;
        }

        private SceneObjectGroup CopyPrim (SceneObjectGroup prim, string name)
        {
            SceneObjectGroup copy = prim.Copy (true);
            copy.Name = name;
            copy.DetachFromBackup ();
            return copy;
        }

        private SceneObjectGroup findByName (string name)
        {
            SceneObjectGroup retVal = null;
            foreach (EntityBase e in m_scene.GetEntities()) {
                if (e.Name == name) {
                    retVal = (SceneObjectGroup)e;
                    break;
                }
            }
            // can't find it so make a default one
            if (retVal == null) {
                m_log.InfoFormat("[{0}]: Prim named {1} was not found in region {2}. Making default wooden sphere.", m_name, name, m_scene.RegionInfo.RegionName);
                retVal = MakeDefaultPrim (name);
            }

            return retVal;
        }

        private SceneObjectGroup MakeDefaultPrim (string name)
        {
            PrimitiveBaseShape shape = PrimitiveBaseShape.CreateSphere ();
            shape.Scale = new Vector3 (0.5f, 0.5f, 0.5f);

            SceneObjectGroup prim = new SceneObjectGroup(m_owner, new Vector3((float)m_scene.RegionInfo.RegionSizeX / 2, (float)m_scene.RegionInfo.RegionSizeY / 2, 25f), shape);
            prim.Name = name;
            prim.DetachFromBackup ();
            m_scene.AddNewSceneObject (prim, false);

            return prim;
        }

    }
}

