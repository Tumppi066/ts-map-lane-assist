﻿using System;
using System.Collections.Generic;
using System.IO;
using TsMap.Common;
using TsMap.Helpers;
using TsMap.Helpers.Logger;
using TsMap.Map.Overlays;

namespace TsMap.TsItem
{
    public class TsPrefabItem : TsItem
    {

        private const int NodeLookBlockSize = 0x3A;
        private const int NodeLookBlockSize825 = 0x38;
        private const int PrefabVegetationBlockSize = 0x20;
        private const double PREFAB_QUALITY = 0.5; // Points per meter
        private const int MIN_QUALITY = 2; // Need two points to make a line
        public ulong FerryUid { get; private set; }
        public int Origin { get; private set; }
        public int Padding { get; private set; }
        public int Origin2 { get; private set; }
        public TsPrefab Prefab { get; private set; }
        private List<TsPrefabLook> _looks;
        public bool IsSecret { get; private set; }

        public void AddLook(TsPrefabLook look)
        {
            _looks.Add(look);
        }

        public List<TsPrefabLook> GetLooks()
        {
            return _looks;
        }

        public bool HasLooks()
        {
            return _looks != null && _looks.Count != 0;
        }

        public TsPrefabItem(TsSector sector, int startOffset) : base(sector, startOffset)
        {
            Navigation = new Dictionary<TsPrefabItem, Tuple<float, List<TsItem>>>();
            Valid = true;
            _looks = new List<TsPrefabLook>();
            Nodes = new List<ulong>();
            if (Sector.Version < 829)
                TsPrefabItem825(startOffset);
            else if (Sector.Version >= 829 && Sector.Version < 831)
                TsPrefabItem829(startOffset);
            else if (Sector.Version >= 831 && Sector.Version < 846)
                TsPrefabItem831(startOffset);
            else if (Sector.Version >= 846 && Sector.Version < 854)
                TsPrefabItem846(startOffset);
            else if (Sector.Version == 854)
                TsPrefabItem854(startOffset);
            else if (Sector.Version >= 855)
                TsPrefabItem855(startOffset);
            else
                Logger.Instance.Error($"Unknown base file version ({Sector.Version}) for item {Type} " +
                    $"in file '{Path.GetFileName(Sector.FilePath)}' @ {startOffset} from '{Sector.GetUberFile().Entry.GetArchiveFile().GetPath()}'");
        }

        public void TsPrefabItem825(int startOffset)
        {
            var fileOffset = startOffset + 0x34; // Set position at start of flags
            DlcGuard = MemoryHelper.ReadUint8(Sector.Stream, fileOffset + 0x01);
            Hidden = (MemoryHelper.ReadUint8(Sector.Stream, fileOffset + 0x02) & 0x02) != 0;
            
            var prefabId = MemoryHelper.ReadUInt64(Sector.Stream, fileOffset += 0x05); // 0x05(flags)
            Prefab = Sector.Mapper.LookupPrefab(prefabId);
            if (Prefab == null)
            {
                Valid = false;
                Logger.Instance.Error($"Could not find Prefab: '{ScsToken.TokenToString(prefabId)}'({prefabId:X}), item uid: 0x{Uid:X}, " +
                        $"in {Path.GetFileName(Sector.FilePath)} @ {fileOffset} from '{Sector.GetUberFile().Entry.GetArchiveFile().GetPath()}'");
            }
            var nodeCount = MemoryHelper.ReadInt32(Sector.Stream, fileOffset += 0x18); // 0x18(id & look & variant)
            fileOffset += 0x04; // set cursor after nodeCount
            for (var i = 0; i < nodeCount; i++)
            {
                Nodes.Add(MemoryHelper.ReadUInt64(Sector.Stream, fileOffset));
                fileOffset += 0x08;
            }

            var connectedItemCount = MemoryHelper.ReadInt32(Sector.Stream, fileOffset);
            Origin = MemoryHelper.ReadUint8(Sector.Stream, fileOffset += 0x04 + (0x08 * connectedItemCount) + 0x08); // 0x04(connItemCount) + connItemUids + 0x08(m_some_uid)
            var prefabVegetationCount = MemoryHelper.ReadInt32(Sector.Stream,
                fileOffset += 0x01 + 0x01 + (NodeLookBlockSize825 * nodeCount)); // 0x01(origin) + 0x01(padding) + nodeLooks
            var vegetationSphereCount = MemoryHelper.ReadInt32(Sector.Stream,
                fileOffset += 0x04 + (PrefabVegetationBlockSize * prefabVegetationCount) + 0x04); // 0x04(prefabVegCount) + prefabVegs + 0x04(padding2)
            fileOffset += 0x04 + (VegetationSphereBlockSize825 * vegetationSphereCount); // 0x04(vegSphereCount) + vegSpheres


            BlockSize = fileOffset - startOffset;
        }

        public void TsPrefabItem829(int startOffset)
        {
            var fileOffset = startOffset + 0x34; // Set position at start of flags
            DlcGuard = MemoryHelper.ReadUint8(Sector.Stream, fileOffset + 0x01);
            Hidden = (MemoryHelper.ReadUint8(Sector.Stream, fileOffset + 0x02) & 0x02) != 0;

            var prefabId = MemoryHelper.ReadUInt64(Sector.Stream, fileOffset += 0x05); // 0x05(flags)
            Prefab = Sector.Mapper.LookupPrefab(prefabId);
            if (Prefab == null)
            {
                Valid = false;
                Logger.Instance.Error($"Could not find Prefab: '{ScsToken.TokenToString(prefabId)}'({prefabId:X}), item uid: 0x{Uid:X}, " +
                        $"in {Path.GetFileName(Sector.FilePath)} @ {fileOffset} from '{Sector.GetUberFile().Entry.GetArchiveFile().GetPath()}'");
            }

            var additionalPartsCount = MemoryHelper.ReadInt32(Sector.Stream, fileOffset += 0x18); // 0x18(id & look & variant)
            var nodeCount = MemoryHelper.ReadUint8(Sector.Stream, fileOffset += 0x04 + (0x08 * additionalPartsCount)); // 0x04(addPartsCount) + additionalParts
            fileOffset += 0x04; // set cursor after nodeCount
            for (var i = 0; i < nodeCount; i++)
            {
                Nodes.Add(MemoryHelper.ReadUInt64(Sector.Stream, fileOffset));
                fileOffset += 0x08;
            }

            var connectedItemCount = MemoryHelper.ReadInt32(Sector.Stream, fileOffset);
            Origin = MemoryHelper.ReadUint8(Sector.Stream, fileOffset += 0x04 + (0x08 * connectedItemCount) + 0x08); // 0x04(connItemCount) + connItemUids + 0x08(m_some_uid)
            var prefabVegetationCount = MemoryHelper.ReadInt32(Sector.Stream,
                fileOffset += 0x01 + 0x01 + (NodeLookBlockSize825 * nodeCount)); // 0x01(origin) + 0x01(padding) + nodeLooks
            var vegetationSphereCount = MemoryHelper.ReadInt32(Sector.Stream,
                fileOffset += 0x04 + (PrefabVegetationBlockSize * prefabVegetationCount) + 0x04); // 0x04(prefabVegCount) + prefabVegs + 0x04(padding2)
            fileOffset += 0x04 + (VegetationSphereBlockSize * vegetationSphereCount); // 0x04(vegSphereCount) + vegSpheres


            BlockSize = fileOffset - startOffset;
        }

        public void TsPrefabItem831(int startOffset)
        {
            var fileOffset = startOffset + 0x34; // Set position at start of flags
            DlcGuard = MemoryHelper.ReadUint8(Sector.Stream, fileOffset + 0x01);
            Hidden = (MemoryHelper.ReadUint8(Sector.Stream, fileOffset + 0x02) & 0x02) != 0;

            var prefabId = MemoryHelper.ReadUInt64(Sector.Stream, fileOffset += 0x05); // 0x05(flags)
            Prefab = Sector.Mapper.LookupPrefab(prefabId);
            if (Prefab == null)
            {
                Valid = false;
                Logger.Instance.Error($"Could not find Prefab: '{ScsToken.TokenToString(prefabId)}'({prefabId:X}), item uid: 0x{Uid:X}, " +
                        $"in {Path.GetFileName(Sector.FilePath)} @ {fileOffset} from '{Sector.GetUberFile().Entry.GetArchiveFile().GetPath()}'");
            }

            var additionalPartsCount = MemoryHelper.ReadInt32(Sector.Stream, fileOffset += 0x18); // 0x18(id & look & variant)
            var nodeCount = MemoryHelper.ReadUint8(Sector.Stream, fileOffset += 0x04 + (0x08 * additionalPartsCount)); // 0x04(addPartsCount) + additionalParts
            fileOffset += 0x04; // set cursor after nodeCount
            for (var i = 0; i < nodeCount; i++)
            {
                Nodes.Add(MemoryHelper.ReadUInt64(Sector.Stream, fileOffset));
                fileOffset += 0x08;
            }

            var connectedItemCount = MemoryHelper.ReadInt32(Sector.Stream, fileOffset);
            Origin = MemoryHelper.ReadUint8(Sector.Stream, fileOffset += 0x04 + (0x08 * connectedItemCount) + 0x08); // 0x04(connItemCount) + connItemUids + 0x08(m_some_uid)
            var prefabVegetationCount = MemoryHelper.ReadInt32(Sector.Stream,
                fileOffset += 0x01 + 0x01 + (NodeLookBlockSize825 * nodeCount)); // 0x01(origin) + 0x01(padding) + nodeLooks
            var vegetationSphereCount = MemoryHelper.ReadInt32(Sector.Stream,
                fileOffset += 0x04 + (PrefabVegetationBlockSize * prefabVegetationCount) + 0x04); // 0x04(prefabVegCount) + prefabVegs + 0x04(padding2)
            fileOffset += 0x04 + (VegetationSphereBlockSize * vegetationSphereCount) + (0x18 * nodeCount); // 0x04(vegSphereCount) + vegSpheres + padding
            BlockSize = fileOffset - startOffset;
        }
        public void TsPrefabItem846(int startOffset)
        {
            var fileOffset = startOffset + 0x34; // Set position at start of flags
            DlcGuard = MemoryHelper.ReadUint8(Sector.Stream, fileOffset + 0x01);
            Hidden = (MemoryHelper.ReadUint8(Sector.Stream, fileOffset + 0x02) & 0x02) != 0;

            var prefabId = MemoryHelper.ReadUInt64(Sector.Stream, fileOffset += 0x05); // 0x05(flags)
            Prefab = Sector.Mapper.LookupPrefab(prefabId);
            if (Prefab == null)
            {
                Valid = false;
                Logger.Instance.Error($"Could not find Prefab: '{ScsToken.TokenToString(prefabId)}'({prefabId:X}), item uid: 0x{Uid:X}, " +
                        $"in {Path.GetFileName(Sector.FilePath)} @ {fileOffset} from '{Sector.GetUberFile().Entry.GetArchiveFile().GetPath()}'");
            }

            var additionalPartsCount = MemoryHelper.ReadInt32(Sector.Stream, fileOffset += 0x18); // 0x18(id & look & variant)
            var nodeCount = MemoryHelper.ReadUint8(Sector.Stream, fileOffset += 0x04 + (0x08 * additionalPartsCount)); // 0x04(addPartsCount) + additionalParts
            fileOffset += 0x04; // set cursor after nodeCount
            for (var i = 0; i < nodeCount; i++)
            {
                Nodes.Add(MemoryHelper.ReadUInt64(Sector.Stream, fileOffset));
                fileOffset += 0x08;
            }

            var connectedItemCount = MemoryHelper.ReadInt32(Sector.Stream, fileOffset);
            Origin = MemoryHelper.ReadUint8(Sector.Stream, fileOffset += 0x04 + (0x08 * connectedItemCount) + 0x08); // 0x04(connItemCount) + connItemUids + 0x08(m_some_uid)
            var prefabVegetationCount = MemoryHelper.ReadInt32(Sector.Stream,
                fileOffset += 0x01 + 0x01 + (NodeLookBlockSize * nodeCount)); // 0x01(origin) + 0x01(padding) + nodeLooks
            var vegetationSphereCount = MemoryHelper.ReadInt32(Sector.Stream,
                fileOffset += 0x04 + (PrefabVegetationBlockSize * prefabVegetationCount) + 0x04); // 0x04(prefabVegCount) + prefabVegs + 0x04(padding2)
            fileOffset += 0x04 + (VegetationSphereBlockSize * vegetationSphereCount) + (0x18 * nodeCount); // 0x04(vegSphereCount) + vegSpheres + padding
            BlockSize = fileOffset - startOffset;
        }

        public void TsPrefabItem854(int startOffset)
        {
            var fileOffset = startOffset + 0x34; // Set position at start of flags
            DlcGuard = MemoryHelper.ReadUint8(Sector.Stream, fileOffset + 0x01);
            Hidden = (MemoryHelper.ReadUint8(Sector.Stream, fileOffset + 0x02) & 0x02) != 0;

            var prefabId = MemoryHelper.ReadUInt64(Sector.Stream, fileOffset += 0x05); // 0x05(flags)
            Prefab = Sector.Mapper.LookupPrefab(prefabId);
            if (Prefab == null)
            {
                Valid = false;
                Logger.Instance.Error($"Could not find Prefab: '{ScsToken.TokenToString(prefabId)}'({prefabId:X}), item uid: 0x{Uid:X}, " +
                        $"in {Path.GetFileName(Sector.FilePath)} @ {fileOffset} from '{Sector.GetUberFile().Entry.GetArchiveFile().GetPath()}'");
            }
            var additionalPartsCount = MemoryHelper.ReadInt32(Sector.Stream, fileOffset += 0x08 + 0x08); // 0x08(prefabId) + 0x08(m_variant)
            var nodeCount = MemoryHelper.ReadInt32(Sector.Stream, fileOffset += 0x04 + (additionalPartsCount * 0x08)); // 0x04(addPartsCount) + additionalParts
            fileOffset += 0x04; // set cursor after nodeCount
            for (var i = 0; i < nodeCount; i++)
            {
                Nodes.Add(MemoryHelper.ReadUInt64(Sector.Stream, fileOffset));
                fileOffset += 0x08;
            }
            var connectedItemCount = MemoryHelper.ReadInt32(Sector.Stream, fileOffset);
            Origin = MemoryHelper.ReadUint8(Sector.Stream, fileOffset += 0x04 + (0x08 * connectedItemCount) + 0x08); // 0x04(connItemCount) + connItemUids + 0x08(m_some_uid)
            fileOffset += 0x02 + nodeCount * 0x0C; // 0x02(origin & padding) + nodeLooks

            BlockSize = fileOffset - startOffset;
        }
        public void TsPrefabItem855(int startOffset)
        {
            var fileOffset = startOffset + 0x34; // Set position at start of flags
            DlcGuard = MemoryHelper.ReadUint8(Sector.Stream, fileOffset + 0x01);
            Hidden = (MemoryHelper.ReadUint8(Sector.Stream, fileOffset + 0x02) & 0x02) != 0;
            IsSecret = MemoryHelper.IsBitSet(MemoryHelper.ReadUint8(Sector.Stream, fileOffset), 5);

            var prefabId = MemoryHelper.ReadUInt64(Sector.Stream, fileOffset += 0x05); // 0x05(flags)
            Prefab = Sector.Mapper.LookupPrefab(prefabId);
            if (Prefab == null)
            {
                Valid = false;
                Logger.Instance.Error($"Could not find Prefab: '{ScsToken.TokenToString(prefabId)}'({prefabId:X}), item uid: 0x{Uid:X}, " +
                        $"in {Path.GetFileName(Sector.FilePath)} @ {fileOffset} from '{Sector.GetUberFile().Entry.GetArchiveFile().GetPath()}'");
            }
            var additionalPartsCount = MemoryHelper.ReadInt32(Sector.Stream, fileOffset += 0x08 + 0x08); // 0x08(prefabId) + 0x08(m_variant)
            var nodeCount = MemoryHelper.ReadInt32(Sector.Stream, fileOffset += 0x04 + (additionalPartsCount * 0x08)); // 0x04(addPartsCount) + additionalParts
            fileOffset += 0x04; // set cursor after nodeCount
            for (var i = 0; i < nodeCount; i++)
            {
                Nodes.Add(MemoryHelper.ReadUInt64(Sector.Stream, fileOffset));
                fileOffset += 0x08;
            }
            var connectedItemCount = MemoryHelper.ReadInt32(Sector.Stream, fileOffset);
            FerryUid = MemoryHelper.ReadUInt64(Sector.Stream, fileOffset + 0x04 + (0x08 * connectedItemCount));
            Origin = MemoryHelper.ReadUint8(Sector.Stream, fileOffset += 0x04 + (0x08 * connectedItemCount) + 0x08); // 0x04(connItemCount) + connItemUids + 0x08(Ferry Uid)
            //Origin2 = MemoryHelper.ReadInt8(Sector.Stream, fileOffset += 0x01);
            //Padding = MemoryHelper.ReadInt8(Sector.Stream, fileOffset + 0x01);
            fileOffset += 0x02 + nodeCount * 0x0C + 0x08; // 0x02(origin & padding) + nodeLooks + 0x08(padding2)
            Padding = MemoryHelper.ReadInt32(Sector.Stream, fileOffset - 0x08);
            BlockSize = fileOffset - startOffset;
        }

        public TsPrefabNode GetNearestNode(TsMapper _mapper, TsNode item, int mode)
        {
            // Mode: 0 -> Only Input Points ; 1 -> Only Output Points ; whatever else -> All Points
            TsPrefabNode node = default(TsPrefabNode);
            node.id = -1;
            float min = float.MaxValue;
            var originNode = _mapper.GetNodeByUid(this.Nodes[0]);
            var mapPointOrigin = this.Prefab.PrefabNodes[this.Origin];
            var prefabStartX = originNode.X - mapPointOrigin.X;
            var prefabStartZ = originNode.Z - mapPointOrigin.Z;
            var rot = (float)(originNode.Rotation - Math.PI - Math.Atan2(mapPointOrigin.RotZ, mapPointOrigin.RotX) + Math.PI / 2);
            foreach (var nod in this.Prefab.PrefabNodes)
            {
                if (nod.InputPoints.Count <= 0 && mode == 0) continue;
                if (nod.OutputPoints.Count <= 0 && mode == 1) continue;
                var newPoint = RenderHelper.RotatePoint(prefabStartX + nod.X, prefabStartZ + nod.Z, rot, originNode.X, originNode.Z);
                float dist = (float)Math.Sqrt(Math.Pow(item.X - (newPoint.X), 2) + Math.Pow(item.Z - (newPoint.Y), 2));
                if (dist < min && dist < 0.2)
                {
                    node = nod;
                    min = dist;
                }
            }
            return node;
        }

        public TsNode NodeIteminPrefab(TsMapper _mapper, TsItem item)
        {
            foreach (var nodePId in this.Nodes)
            {
                var nodeP = _mapper.GetNodeByUid(nodePId);
                if (nodeP.ForwardItem == item) return nodeP;
                if (nodeP.BackwardItem == item) return nodeP;
            }
            return null;
        }

        public List<Tuple<TsNode, TsPrefabItem>> NodePrefabinPrefab(TsMapper _mapper)
        {
            List<Tuple<TsNode, TsPrefabItem>> prefabs = new List<Tuple<TsNode, TsPrefabItem>>();
            foreach (var nodePId in this.Nodes)
            {
                var nodeP = _mapper.GetNodeByUid(nodePId);
                if (nodeP.ForwardItem != null && nodeP.ForwardItem.Type == TsItemType.Prefab && nodeP.ForwardItem != this) prefabs.Add(new Tuple<TsNode, TsPrefabItem>(nodeP, (TsPrefabItem)nodeP.ForwardItem));
                if (nodeP.BackwardItem != null && nodeP.BackwardItem.Type == TsItemType.Prefab && nodeP.BackwardItem != this) prefabs.Add(new Tuple<TsNode, TsPrefabItem>(nodeP, (TsPrefabItem)nodeP.BackwardItem));
            }
            return prefabs;
        }

        // Can be useful for A* Algorithm
        public float HeuristicDistance(TsItem item)
        {
            return Math.Abs(this.X - item.X) + Math.Abs(this.Z - item.Z);
        }

        // Copied from TsRoadLook.cs... used for the prefab curve calculations.
        public static double Hermite(float s, float x, float z, double tanX, double tanZ)
        {
            double h1 = 2 * Math.Pow(s, 3) - 3 * Math.Pow(s, 2) + 1;
            double h2 = -2 * Math.Pow(s, 3) + 3 * Math.Pow(s, 2);
            double h3 = Math.Pow(s, 3) - 2 * Math.Pow(s, 2) + s;
            double h4 = Math.Pow(s, 3) - Math.Pow(s, 2);
            return h1 * x + h2 * z + h3 * tanX + h4 * tanZ;
        }

        internal override void Update()
        {
            var originNode = Sector.Mapper.GetNodeByUid(Nodes[0]);
            if (Prefab?.PrefabNodes == null) return;

            var mapPointOrigin = Prefab.PrefabNodes[Origin];

            var rot = (float)(originNode.Rotation - Math.PI -
                Math.Atan2(mapPointOrigin.RotZ, mapPointOrigin.RotX) + Math.PI / 2);

            var prefabStartX = originNode.X - mapPointOrigin.X;
            var prefabStartZ = originNode.Z - mapPointOrigin.Z;
            var prefabStartY = originNode.Y - mapPointOrigin.Y;
            foreach (var spawnPoint in Prefab.SpawnPoints)
            {
                var newPoint = RenderHelper.RotatePoint(prefabStartX + spawnPoint.X, prefabStartZ + spawnPoint.Z, rot,
                    originNode.X, originNode.Z);

                var overlayName = "";
                var displayName = "";

                if (spawnPoint.Type == TsSpawnPointType.GasPos)
                {
                    overlayName = "gas_ico";
                    displayName = "Fuel";
                }

                else if (spawnPoint.Type == TsSpawnPointType.ServicePos)
                {
                    overlayName = "service_ico";
                    displayName = "Service";
                }
                else if (spawnPoint.Type == TsSpawnPointType.WeightStationPos)
                {
                    overlayName = "weigh_station_ico";
                    displayName = "WeightStation";
                }
                else if (spawnPoint.Type == TsSpawnPointType.TruckDealerPos)
                {
                    overlayName = "dealer_ico";
                    displayName = "TruckDealer";
                }
                else if (spawnPoint.Type == TsSpawnPointType.BuyPos)
                {
                    overlayName = "garage_large_ico";
                    displayName = "Garage";
                }
                else if (spawnPoint.Type == TsSpawnPointType.RecruitmentPos)
                {
                    overlayName = "recruitment_ico";
                    displayName = "Recruitment";
                }

                Sector.Mapper.OverlayManager.AddOverlay(overlayName, OverlayType.Map, newPoint.X, newPoint.Y,
                    displayName, DlcGuard, IsSecret);
            }

            var lastId = -1;
            foreach (var triggerPoint in Prefab.TriggerPoints) // trigger points in prefabs: garage, hotel, ...
            {
                var newPoint = RenderHelper.RotatePoint(prefabStartX + triggerPoint.X, prefabStartZ + triggerPoint.Z,
                    rot,
                    originNode.X, originNode.Z);

                if (triggerPoint.TriggerId == lastId) continue;
                lastId = (int)triggerPoint.TriggerId;

                if (triggerPoint.TriggerActionToken == ScsToken.StringToToken("hud_parking")) // parking trigger
                {
                    Sector.Mapper.OverlayManager.AddOverlay("parking_ico", OverlayType.Map, newPoint.X, newPoint.Y,
                        "Parking", DlcGuard, IsSecret);
                }
            }
        }
    }
}
