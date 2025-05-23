using System;
using System.Collections.Generic;
using System.Linq;
using ChestContents.Effects;
using ChestContents.Models;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChestContents.Managers
{
    public class IndicatedChestList
    {
        private readonly HashSet<int> _chestSet;
        private readonly ActionableEffect _effect;
        private readonly Dictionary<int, GameObject> _verticalIndicators = new Dictionary<int, GameObject>();
        private GameObject _activeConnectionVfx;

        public IndicatedChestList()
        {
            ChestList = new List<ChestInfo>();
            _chestSet = new HashSet<int>();
            _effect = new ActionableEffect("vfx_ExtensionConnection");
        }

        public IndicatedChestList(List<ChestInfo> chestList, ActionableEffect effect)
        {
            ChestList = chestList;
            _chestSet = new HashSet<int>(chestList.Select(c => c.InstanceID));
            _effect = effect;
        }

        public IndicatedChestList(List<ChestInfo> chestList)
        {
            ChestList = chestList;
            _chestSet = new HashSet<int>(chestList.Select(c => c.InstanceID));
            _effect = new ActionableEffect("vfx_ExtensionConnection");
        }

        public List<ChestInfo> ChestList { get; }

        public void Add(ChestInfo chest, bool unique = true)
        {
            if (unique)
            {
                if (_chestSet.Add(chest.InstanceID)) ChestList.Add(chest);
            }
            else
            {
                ChestList.Add(chest);
                _chestSet.Add(chest.InstanceID);
            }
        }

        public void Clear()
        {
            ChestList.Clear();
            _chestSet.Clear();
            _effect.PurgeInvalid(new HashSet<int>()); // Also clear all VFX
        }

        public void PurgeInvalid(HashSet<int> validInstanceIds)
        {
            ChestList.RemoveAll(ci => !validInstanceIds.Contains(ci.InstanceID));
            _chestSet.RemoveWhere(id => !validInstanceIds.Contains(id));
            _effect.PurgeInvalid(validInstanceIds); // Clean up VFX for removed chests
        }

        public void RunEffects()
        {
            if (Game.IsPaused()) return;
            var time = (float)DateTime.Now.TimeOfDay.TotalSeconds;
            foreach (var chest in ChestList)
            {
                var radius = 0.5f;
                var speed = 2f;
                var offset = new Vector3(Mathf.Cos(time * speed), 0, Mathf.Sin(time * speed)) * radius;
                var vfxPos = chest.Position + offset;
                _effect.RunEffect(vfxPos, chest.Rotation, chest.InstanceID);

                if (!_verticalIndicators.ContainsKey(chest.InstanceID) || _verticalIndicators[chest.InstanceID] == null)
                {
                    var indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    indicator.name = $"ChestVerticalIndicator_{chest.InstanceID}";
                    Object.Destroy(indicator.GetComponent<Collider>());
                    indicator.transform.localScale = new Vector3(0.15f, 3f, 0.15f);
                    var shader = Shader.Find("Sprites/Default");
                    var mat = new Material(shader);
                    mat.color = new Color(1f, 0.5f, 0f, 0.5f);
                    indicator.GetComponent<Renderer>().material = mat;
                    _verticalIndicators[chest.InstanceID] = indicator;
                }

                var ind = _verticalIndicators[chest.InstanceID];
                ind.transform.position = chest.Position + new Vector3(0, 3f, 0);
                ind.transform.rotation = Quaternion.identity;
                ind.SetActive(true);
            }

            var validIds = new HashSet<int>(ChestList.Select(c => c.InstanceID));
            var toRemove = _verticalIndicators.Keys.Where(id => !validIds.Contains(id)).ToList();
            foreach (var id in toRemove)
            {
                if (_verticalIndicators[id] != null)
                    Object.Destroy(_verticalIndicators[id]);
                _verticalIndicators.Remove(id);
            }

            if (ChestList.Count > 0 && Player.m_localPlayer != null)
            {
                var playerPos = Player.m_localPlayer.transform.position;
                var chestPos = ChestList[0].Position;
                playerPos.y += 1.5f;
                chestPos.y += 1.5f;

                if (_activeConnectionVfx == null)
                {
                    _activeConnectionVfx = new GameObject("ChestConnectionLine");
                    var line = _activeConnectionVfx.AddComponent<LineRenderer>();
                    line.material = new Material(Shader.Find("Sprites/Default"));
                    line.widthMultiplier = 0.1f;
                    line.positionCount = 2;
                    line.useWorldSpace = true;
                    line.startColor = Color.cyan;
                    line.endColor = Color.yellow;
                }

                if (_activeConnectionVfx != null)
                {
                    var line = _activeConnectionVfx.GetComponent<LineRenderer>();
                    if (line != null)
                    {
                        line.SetPosition(0, playerPos);
                        line.SetPosition(1, chestPos);
                    }
                }
            }
            else if (_activeConnectionVfx != null)
            {
                Object.Destroy(_activeConnectionVfx);
                _activeConnectionVfx = null;
            }
        }
    }
}