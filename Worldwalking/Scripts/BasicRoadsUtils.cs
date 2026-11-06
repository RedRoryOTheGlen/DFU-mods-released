using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallConnect.Arena2;

namespace WorldwalkingMod.Compatibility
{
    public struct RoadData
    {
        public Vector4[] NW_NE_SW_SE;
        public Vector4[] N_E_S_W;
    }
    public static class BasicRoadsUtils
    {
        const byte N = 128;//0b_1000_0000;
        const byte NE = 64; //0b_0100_0000;
        const byte E = 32; //0b_0010_0000;
        const byte SE = 16; //0b_0001_0000;
        const byte S = 8;  //0b_0000_1000;
        const byte SW = 4;  //0b_0000_0100;
        const byte W = 2;  //0b_0000_0010;
        const byte NW = 1;  //0b_0000_0001;

        const int roads = 0;
        const int tracks = 1;
        const int rivers = 2;
        const int streams = 3;
        const string GET_PATH_DATA = "getPathData";
        static byte[][] pathsData = new byte[4][];

        public static void Init()
        {
            if (Worldwalking.Instance.BasicRoads == null)
                return;

            pathsData[roads] = GetPathData(roads);
            pathsData[tracks] = GetPathData(tracks);
            //pathsData[rivers] = GetPathData(rivers);
            //pathsData[streams] = GetPathData(streams);
        }

         public static RoadData GetRoadData(int mapPixelX, int mapPixelY)
        {
            var roadData = new RoadData
            {
                NW_NE_SW_SE = new Vector4[9],
                N_E_S_W = new Vector4[9]
            };

            if (Worldwalking.Instance.BasicRoads == null)
                return roadData;

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    var mpx = mapPixelX + x;
                    var mpy = mapPixelY + y;

                    if (mpx < 0 || mpx >= MapsFile.MaxMapPixelX)
                        continue;
                    if (mpy < 0 || mpy >= MapsFile.MaxMapPixelY)
                        continue;

                    var i = mpx + mpy * MapsFile.MaxMapPixelX;
                    var si = (x + 1) + (y + 1) * 3;

                    //Debug.Log("BASIC ROADS UTILS - INDEX IS " + si.ToString());

                    roadData.NW_NE_SW_SE[si] = new Vector4()
                    {
                        x = HasRoadPoint(i, NW) ? 1 : 0,
                        y = HasRoadPoint(i, NE) ? 1 : 0,
                        z = HasRoadPoint(i, SW) ? 1 : 0,
                        w = HasRoadPoint(i, SE) ? 1 : 0,
                    };

                    roadData.N_E_S_W[si] = new Vector4()
                    {
                        x = HasRoadPoint(i, N) ? 1 : 0,
                        y = HasRoadPoint(i, E) ? 1 : 0,
                        z = HasRoadPoint(i, S) ? 1 : 0,
                        w = HasRoadPoint(i, W) ? 1 : 0,
                    };

                    // Add debug messages here to log the computed vectors
                    /*Debug.Log($"NW_NE_SW_SE[{si}]: {roadData.NW_NE_SW_SE[si]}");
                    Debug.Log($"N_E_S_W[{si}]: {roadData.N_E_S_W[si]}");*/
                }
            }

            return roadData;
         }


        private static bool HasRoadPoint(int i, byte direction)
        {
            var hasRoadPoint = false;

            if ((pathsData[roads][i] & direction) != 0)
                hasRoadPoint = true;
            if ((pathsData[tracks][i] & direction) != 0)
                hasRoadPoint = true;
            /*if (pathsData[rivers][i] % direction != 0)
                hasRoadPoint = true;
            if (pathsData[streams][i] % direction != 0)
                hasRoadPoint = true;*/

            return hasRoadPoint;
        }

        public static bool IsRoad(int mapPixelX, int mapPixelY, bool includeTracks = true)
        {
            bool isRoad = false;

            if (Worldwalking.Instance.BasicRoads == null)
                return isRoad;

            if (mapPixelX < 0 || mapPixelX >= MapsFile.MaxMapPixelX)
                return isRoad;
            if (mapPixelY < 0 || mapPixelY >= MapsFile.MaxMapPixelY)
                return isRoad;

            int mpx = mapPixelX + mapPixelY * MapsFile.MaxMapPixelX;

            if (pathsData[roads][mpx] != 0)
                isRoad = true;  
            if (includeTracks && pathsData[tracks][mpx] != 0)
                isRoad = true;

            return isRoad;
        }

        public static int GetRoadType(int mapPixelX, int mapPixelY)
        {
            int tile = 0;

            if (Worldwalking.Instance.BasicRoads == null)
                return tile;

            if (mapPixelX < 0 || mapPixelX >= MapsFile.MaxMapPixelX)
                return tile;
            if (mapPixelY < 0 || mapPixelY >= MapsFile.MaxMapPixelY)
                return tile;

            int mpx = mapPixelX + mapPixelY * MapsFile.MaxMapPixelX;

            if (pathsData[roads][mpx] != 0)
                tile = 1;
            else if (pathsData[tracks][mpx] != 0)
                tile = 2;

            return tile;
        }

        public static List<Vector3> GetRoadDirections(RoadData roadData)
        {
            List<Vector3> directions = new List<Vector3>();

            if (roadData.N_E_S_W[4].x != 0)
                directions.Add(Vector3.up);
            if (roadData.N_E_S_W[4].y != 0)
                directions.Add(Vector3.right);
            if (roadData.N_E_S_W[4].z != 0)
                directions.Add(Vector3.down);
            if (roadData.N_E_S_W[4].w != 0)
                directions.Add(Vector3.left);

            if (roadData.NW_NE_SW_SE[4].x != 0)
                directions.Add(Vector3.up + Vector3.left);
            if (roadData.NW_NE_SW_SE[4].y != 0)
                directions.Add(Vector3.up + Vector3.right);
            if (roadData.NW_NE_SW_SE[4].z != 0)
                directions.Add(Vector3.down + Vector3.left);
            if (roadData.NW_NE_SW_SE[4].w != 0)
                directions.Add(Vector3.down + Vector3.right);

            return directions;
        }

        private static byte[] GetPathData(int type)
        {
            var modName = Worldwalking.Instance.BasicRoads.Title;
            byte[] pathData = null;

            ModManager.Instance.SendModMessage(modName, GET_PATH_DATA, type, (string message, object data) =>
            {
                pathData = data as byte[];
            });

            return pathData;
        }
    }
}
