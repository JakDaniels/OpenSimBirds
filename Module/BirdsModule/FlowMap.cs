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
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;

namespace Flocking
{
    public class FlowMap
    {
        private Scene m_scene;
        private float[, ,] m_flowMap;
        private uint regionX;
        private uint regionY;
        private uint regionZ;
        private float regionBorder;

        public FlowMap (Scene scene, int maxHeight, float borderSize)
        {
            m_scene = scene;
            regionX = m_scene.RegionInfo.RegionSizeX;
            regionY = m_scene.RegionInfo.RegionSizeY;
            regionZ = (uint)maxHeight;
            regionBorder = borderSize;
            m_flowMap = new float[regionX, regionY, regionZ];
        }

        public int LengthX {
            get {return (int)regionX;}
        }
        public int LengthY {
            get {return (int)regionY;}
        }
        public int LengthZ {
            get {return (int)regionZ;}
        }
        public int Border  {
            get {return (int)regionBorder;}
        }

        public void Initialise() {
            //fill in the boundaries
            for( int x = 0; x < regionX; x++ ) {
                for( int y = 0; y < regionY; y++ ) {
                    m_flowMap[x,y,0] = 100f;
                    m_flowMap[x,y, regionZ-1] = 100f;
                }
            }
            for( int x = 0; x < regionX; x++ ) {
                for( int z = 0; z < regionZ; z++ ) {
                    m_flowMap[x,0,z] = 100f;
                    m_flowMap[x,regionY-1,z] = 100f;
                }
            }
            for( int y = 0; y < regionY; y++ ) {
                for( int z = 0; z < regionZ; z++ ) {
                    m_flowMap[0,y,z] = 100f;
                    m_flowMap[regionX-1,y,z] = 100f;
                }
            }

            //fill in the terrain
            for( int x = 0; x < regionX; x++ ) {
                for( int y = 0; y < regionY; y++ ) {
                    int zMax = Convert.ToInt32(m_scene.GetGroundHeight( x, y ));
                    for( int z = 1; z < zMax; z++ ) {
                        m_flowMap[x,y,z] = 100f;
                    }
                }
            }

            // fill in the things
            foreach( EntityBase entity in m_scene.GetEntities() ) {
                if( entity is SceneObjectGroup ) {
                    SceneObjectGroup sog = (SceneObjectGroup)entity;

                    //todo: ignore phantom
                    float fmaxX, fminX, fmaxY, fminY, fmaxZ, fminZ;
                    int maxX, minX, maxY, minY, maxZ, minZ;
                    sog.GetAxisAlignedBoundingBoxRaw( out fminX, out fmaxX, out fminY, out fmaxY, out fminZ, out fmaxZ );

                    minX = Convert.ToInt32(fminX);
                    maxX = Convert.ToInt32(fmaxX);
                    minY = Convert.ToInt32(fminY);
                    maxY = Convert.ToInt32(fmaxX);
                    minZ = Convert.ToInt32(fminZ);
                    maxZ = Convert.ToInt32(fmaxZ);

                    for( int x = minX; x < maxX; x++ ) {
                        for( int y = minY; y < maxY; y++ ) {
                            for( int z = minZ; z < maxZ; z++ ) {
                                m_flowMap[x,y,z] = 100f;
                            }
                        }
                    }
                }
            }
        }

        public bool WouldHitObstacle (Vector3 currPos, Vector3 targetPos)
        {
            bool retVal = false;
            //fail fast
            if( IsOutOfBounds(targetPos) ) {
                retVal = true;
            } else if( IsWithinObstacle(targetPos) ) {
                retVal = true;
            } else if( IntersectsObstacle (currPos, targetPos) ) {
                retVal = true;
            }

            return retVal;
        }

        public bool IsOutOfBounds(Vector3 targetPos) {
            bool retVal = false;
            if( targetPos.X < regionBorder ||
                targetPos.X > regionX - regionBorder ||
                targetPos.Y < regionBorder ||
                targetPos.Y > regionY - regionBorder ||
                targetPos.Z < regionBorder ||
                targetPos.Z > regionZ - regionBorder ) {

                retVal = true;
            }

            return retVal;
        }

        public bool IntersectsObstacle (Vector3 currPos, Vector3 targetPos)
        {
            bool retVal = false;
            // Ray trace the Vector and fail as soon as we hit something
            Vector3 direction = targetPos - currPos;
            float length = direction.Length();
            // check every metre
            for( float i = 1f; i < length; i += 1f ) {
                Vector3 rayPos = currPos + ( direction * i );
                //give up if we go OOB on this ray
                if( IsOutOfBounds( rayPos ) ){
                    retVal = true;
                    break;
                }
                else if( IsWithinObstacle( rayPos ) ) {
                    retVal = true;
                    break;
                }
            }

            return retVal;
        }

        public bool IsWithinObstacle( Vector3 targetPos ) {
            return IsWithinObstacle(Convert.ToInt32(targetPos.X), Convert.ToInt32(targetPos.Y),Convert.ToInt32(targetPos.Z));
        }

        public bool IsWithinObstacle( int x, int y, int z ) {
            bool retVal = false;
            if (x >= LengthX || y >= LengthY || z >= LengthZ)
            {
                retVal = true;
            }
            else if (x < 0 || y < 0 || z < 0)
            {
                retVal = true;
            } else if (m_flowMap[x,y,z] > 50f) {
                retVal = true;
            }
            return retVal;
        }
    }


}

