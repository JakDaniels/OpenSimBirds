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

namespace Flocking
{
    public class FlockingModel
    {
        private List<Bird> m_flock = new List<Bird>();
        private FlowMap m_flowMap;
        private float m_maxSpeed;
        private float m_maxForce;
        private float m_neighbourDistance;
        private float m_desiredSeparation;
        private float m_tolerance;
        private float m_border;
        private string m_name;

        private Random m_rnd = new Random(Environment.TickCount);

        public int Size {
            get {return m_flock.Count;}
            set {
                //if( value < m_flock.Count ) {
                //  m_flock.RemoveRange( 0, m_flock.Count - value );
                //} else
                m_flock = new List<Bird>();
                while( value > m_flock.Count ) {
                    AddBird(m_name + m_flock.Count);
                }
            }
        }

        public FlockingModel(string moduleName, float maxSpeed, float maxForce, float neighbourDistance, float desiredSeparation, float tolerance, float border) {
            m_name = moduleName;
            m_maxSpeed = maxSpeed;
            m_maxForce = maxForce;
            m_neighbourDistance = neighbourDistance;
            m_desiredSeparation = desiredSeparation;
            m_tolerance = tolerance;
            m_border = border;
        }

        void AddBird (string name)
        {
            Bird Bird = new Bird (name, this, m_flowMap);

            // find an initial random location for this Bird
            // somewhere not within an obstacle
            int xInit = m_rnd.Next(m_flowMap.LengthX);
            int yInit = m_rnd.Next(m_flowMap.LengthY);
            int zInit = m_rnd.Next(m_flowMap.LengthZ);

            while( m_flowMap.IsWithinObstacle( xInit, yInit, zInit ) ){
                xInit = m_rnd.Next(m_flowMap.LengthX);
                yInit = m_rnd.Next(m_flowMap.LengthY);
                zInit = m_rnd.Next(m_flowMap.LengthZ);
            }

            Bird.Location = new Vector3 (Convert.ToSingle(xInit), Convert.ToSingle(yInit), Convert.ToSingle(zInit));
            m_flock.Add (Bird);
        }

        public float MaxSpeed {
            get {return m_maxSpeed;}
            set { m_maxSpeed = value; }
        }

        public float MaxForce {
            get {return m_maxForce;}
            set { m_maxForce = value; }
        }

        public float NeighbourDistance {
            get {return m_neighbourDistance;}
            set { m_neighbourDistance = value; }
        }

        public float DesiredSeparation {
            get {return m_desiredSeparation;}
            set { m_desiredSeparation = value; }
        }

        public float Tolerance {
            get {return m_tolerance;}
            set { m_tolerance = value; }
        }


        public void Initialise (int num, FlowMap flowMap)
        {
            m_flowMap = flowMap;
            for (int i = 0; i < num; i++) {
                AddBird (m_name + i );
            }
        }

        public List<Bird> UpdateFlockPos ()
        {
            foreach (Bird b in m_flock) {
                b.MoveInSceneRelativeToFlock(m_flock);  // Passing the entire list of Birds to each Bird individually
            }

            return m_flock;
        }
    }
}

