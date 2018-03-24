/*
 * Copyright (c) Contributors, https://github.com/jonc/osbirds
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
using log4net;
using OpenMetaverse;

namespace Flocking
{
    public class Bird
    {
        private static readonly ILog m_log = LogManager.GetLogger (System.Reflection.MethodBase.GetCurrentMethod ().DeclaringType);
        private string m_id;

        private Vector3 m_loc;
        private Vector3 m_vel;
        private Vector3 m_acc;
        private Random m_rndnums = new Random (Environment.TickCount);

        private FlockingModel m_model;
        private FlowMap m_flowMap;
        private int m_regionX;
        private int m_regionY;
        private int m_regionZ;
        private float m_regionBorder;

        /// <summary>
        /// Initializes a new instance of the <see cref="Flocking.Bird"/> class.
        /// </summary>
        /// <param name='l'>
        /// L. the initial position of this bird
        /// </param>
        /// <param name='ms'>
        /// Ms. max speed this bird can attain
        /// </param>
        /// <param name='mf'>
        /// Mf. max force / acceleration this bird can extert
        /// </param>
        public Bird (string id, FlockingModel model, FlowMap flowMap)
        {
            m_id = id;
            m_acc = Vector3.Zero;
            m_vel = new Vector3 (m_rndnums.Next (-1, 1), m_rndnums.Next (-1, 1), m_rndnums.Next (-1, 1));
            m_model = model;
            m_flowMap = flowMap;
            m_regionX = m_flowMap.LengthX;
            m_regionY = m_flowMap.LengthY;
            m_regionZ = m_flowMap.LengthZ;
            m_regionBorder = m_flowMap.Border;
        }

        public Vector3 Location {
            get { return m_loc;}
            set { m_loc = value; }
        }

        public Vector3 Velocity {
            get { return m_vel;}
        }

        public String Id {
            get {return m_id;}
        }

        /// <summary>
        /// Moves our bird in the scene relative to the rest of the flock.
        /// </summary>
        /// <param name='birds'>
        /// Birds. all the other chaps in the scene
        /// </param>
        public void MoveInSceneRelativeToFlock (List<Bird> birds)
        {
            // we would like to stay with our mates
            Flock (birds);

            // our first priority is to not hurt ourselves
            AvoidObstacles ();

            // then we want to avoid any threats
            //      this not implemented yet


            // ok so we worked our where we want to go, so ...
            UpdatePositionInScene ();

        }

        /// <summary>
        /// Move within our flock
        ///
        /// We accumulate a new acceleration each time based on three rules
        /// these are:
        /// our separation from our closest neighbours,
        /// our desire to keep travelling within the local flock,
        /// our desire to move towards the flock centre
        ///
        /// </summary>
        void Flock (List<Bird> birds)
        {

            // calc the force vectors on this bird
            Vector3 sep = Separate (birds);   // Separation
            Vector3 ali = Align (birds);      // Alignment
            Vector3 coh = Cohesion (birds);   // Cohesion

            // Arbitrarily weight these forces
            //TODO: expose these consts
            sep *= 1.5f; //.mult(1.5);
            //ali.mult(1.0);
            ali *= 1.0f;
            //coh.mult(1.0);
            coh *= 1.0f;

            // Add the force vectors to the current acceleration of the bird
            //acc.add(sep);
            m_acc += sep;
            //acc.add(ali);
            m_acc += ali;
            //acc.add(coh);
            m_acc += coh;
        }


        /// <summary>
        /// Method to update our location within the scene.
        /// update our location in the world based on our
        /// current location, velocity and acceleration
        /// taking into account our max speed
        ///
        /// </summary>
        void UpdatePositionInScene ()
        {
            // Update velocity
            //vel.add(acc);
            m_vel += m_acc;
            // Limit speed
            //m_vel.limit(maxspeed);
            m_vel = BirdsUtil.Limit (m_vel, m_model.MaxSpeed);
            m_loc += m_vel;
            // Reset accelertion to 0 each cycle
            m_acc *= 0.0f;
        }

        /// <summary>
        /// Seek the specified target. Move into that flock
        /// Accelerate us towards where we want to go
        /// </summary>
        /// <param name='target'>
        /// Target. the position within the flock we would like to achieve
        /// </param>
        void Seek (Vector3 target)
        {
            m_acc += Steer (target, false);
        }

        /// <summary>
        /// Arrive the specified target. Slow us down, as we are almost there
        /// </summary>
        /// <param name='target'>
        /// Target. the flock we would like to think ourselves part of
        /// </param>
        void arrive (Vector3 target)
        {
            m_acc += Steer (target, true);
        }

        /// A method that calculates a steering vector towards a target
        /// Takes a second argument, if true, it slows down as it approaches the target
        Vector3 Steer (Vector3 target, bool slowdown)
        {
            Vector3 steer;  // The steering vector
            Vector3 desired = Vector3.Subtract(target, m_loc);  // A vector pointing from the location to the target
            float d = desired.Length (); // Distance from the target is the magnitude of the vector
            // If the distance is greater than 0, calc steering (otherwise return zero vector)
            if (d > 0) {
                // Normalize desired
                desired.Normalize ();
                // Two options for desired vector magnitude (1 -- based on distance, 2 -- maxspeed)
                if ((slowdown) && (d < 100.0f)) {
                    desired *= (m_model.MaxSpeed * (d / 100.0f)); // This damping is somewhat arbitrary
                } else {
                    desired *= m_model.MaxSpeed;
                }
                // Steering = Desired minus Velocity
                //steer = target.sub(desired,m_vel);
                steer = Vector3.Subtract (desired, m_vel);
                //steer.limit(maxforce);  // Limit to maximum steering force
                steer = BirdsUtil.Limit (steer, m_model.MaxForce);
            } else {
                steer = Vector3.Zero;
            }
            return steer;
        }


        /// <summary>
        /// Borders this instance.
        /// if we get too close wrap us around
        /// CHANGE THIS to navigate away from whatever it is we are too close to
        /// </summary>
        void AvoidObstacles ()
        {
            //look tolerance metres ahead
            Vector3 normVel = Vector3.Normalize(m_vel);
            Vector3 inFront = m_loc + Vector3.Multiply(normVel, m_model.Tolerance);
            if( m_flowMap.WouldHitObstacle( m_loc, inFront ) ) {
                AdjustVelocityToAvoidObstacles ();

            }
        }

        void AdjustVelocityToAvoidObstacles ()
        {
            for( int i = 1; i < 5; i++ ) {
                Vector3 normVel = Vector3.Normalize(m_vel);
                int xDelta = m_rndnums.Next (-i, i);
                int yDelta = m_rndnums.Next (-i, i);
                int zDelta = m_rndnums.Next (-i, i);
                normVel.X += xDelta;
                normVel.Y += yDelta;
                normVel.Z += zDelta;
                Vector3 inFront = m_loc + Vector3.Multiply(normVel, m_model.Tolerance);
                if( !m_flowMap.WouldHitObstacle( m_loc, inFront ) ) {
                    m_vel.X += xDelta;
                    m_vel.Y += yDelta;
                    m_vel.Z += zDelta;
                    //m_log.Info("avoided");
                    return;
                }
            }
            //m_log.Info("didn't avoid");
            // try increaing our acceleration
            // or try decreasing our acceleration
            // or turn around - coz where we came from was OK
            if (m_loc.X < m_regionBorder || m_loc.X > m_regionX - m_regionBorder)
                m_vel.X = -m_vel.X;
            if (m_loc.Y < m_regionBorder || m_loc.Y > m_regionY - m_regionBorder)
                m_vel.Y = -m_vel.Y;
            if (m_loc.Z < 21 || m_loc.Z > m_regionZ )
                m_vel.Z = -m_vel.Z;
        }

        /// <summary>
        /// Separate ourselves from the specified birds.
        /// keeps us a respectable distance from our closest neighbours whilst still
        /// being part of our local flock
        /// </summary>
        /// <param name='birds'>
        /// Birds. all the birds in the scene
        /// </param>
        Vector3 Separate (List<Bird> birds)
        {
            Vector3 steer = new Vector3 (0, 0, 0);
            int count = 0;
            // For every bird in the system, check if it's too close
            foreach (Bird other in birds) {
                float d = Vector3.Distance (m_loc, other.Location);
                // If the distance is greater than 0 and less than an arbitrary amount (0 when you are yourself)
                if ((d > 0) && (d < m_model.DesiredSeparation)) {
                    // Calculate vector pointing away from neighbor
                    Vector3 diff = Vector3.Subtract (m_loc, other.Location);
                    diff.Normalize ();
                    diff = Vector3.Divide (diff, d);
                    steer = Vector3.Add (steer, diff);
                    count++;            // Keep track of how many
                }
            }
            // Average -- divide by how many
            if (count > 0) {
                steer /= (float)count;
            }

            // As long as the vector is greater than 0
            if (steer.Length () > 0) {
                // Implement Reynolds: Steering = Desired - Velocity
                steer.Normalize ();
                steer *= m_model.MaxSpeed;
                steer -= m_vel;
                //steer.limit(maxforce);
                steer = BirdsUtil.Limit (steer, m_model.MaxForce);
            }
            return steer;
        }

        /// <summary>
        /// Align our bird within the flock.
        /// For every nearby bird in the system, calculate the average velocity
        /// and move us towards that - this keeps us moving with the flock.
        /// </summary>
        /// <param name='birds'>
        /// Birds. all the birds in the scene - we only really care about those in the neighbourdist
        /// </param>
        Vector3 Align (List<Bird> birds)
        {
            Vector3 steer = new Vector3 (0, 0, 0);
            int count = 0;
            foreach (Bird other in birds) {
                float d = Vector3.Distance (m_loc, other.Location);
                if ((d > 0) && (d < m_model.NeighbourDistance)) {
                    steer += other.Velocity;
                    count++;
                }
            }
            if (count > 0) {
                steer /= (float)count;
            }

            // As long as the vector is greater than 0
            if (steer.Length () > 0) {
                // Implement Reynolds: Steering = Desired - Velocity
                steer.Normalize ();
                steer *= m_model.MaxSpeed;
                steer -= m_vel;
                //steer.limit(maxforce);
                steer = BirdsUtil.Limit (steer, m_model.MaxForce);

            }
            return steer;
        }

        /// <summary>
        /// MAintain the cohesion of our local flock
        /// For the average location (i.e. center) of all nearby birds, calculate our steering vector towards that location
        /// </summary>
        /// <param name='birds'>
        /// Birds. the birds in the scene
        /// </param>
        Vector3 Cohesion (List<Bird> birds)
        {

            Vector3 sum = Vector3.Zero;   // Start with empty vector to accumulate all locations
            int count = 0;

            foreach (Bird other in birds) {
                float d = Vector3.Distance (m_loc, other.Location);
                if ((d > 0) && (d < m_model.NeighbourDistance)) {
                    sum += other.Location; // Add location
                    count++;
                }
            }
            if (count > 0) {
                sum /= (float)count;
                return Steer (sum, false);  // Steer towards the location
            }
            return sum;
        }
    }
}

