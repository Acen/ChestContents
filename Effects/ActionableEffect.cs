using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;

namespace ChestContents.Effects
{
    public interface IEffectRunner
    {
        void RunEffect(Vector3 position, Quaternion rotation, int chestInstanceID);
        void ShowEffectForTarget(Vector3 position, Quaternion rotation, int uniqueId);
        void ClearEffectForTarget(int uniqueId);
        void PurgeInvalid(HashSet<int> validInstanceIds);
    }

    public class ActionableEffect : IEffectRunner
    {
        private const int EffectsPerChest = 12;

        private readonly Dictionary<int, List<EffectInstance>> _activeEffectInstances =
            new Dictionary<int, List<EffectInstance>>();

        private readonly Dictionary<int, List<GameObject>> _activeEffects = new Dictionary<int, List<GameObject>>();
        private readonly string _prefabName;

        public ActionableEffect(string prefabName)
        {
            _prefabName = prefabName;
        }

        public void RunEffect(Vector3 position, Quaternion rotation, int chestInstanceID)
        {
            position.y += 1.5f;
            var starCount = 12;
            var starRadius = 0.8f;
            var starOffsets = new Vector3[starCount];
            for (var i = 0; i < starCount; i++)
            {
                var angle = i * Mathf.PI * 2f / starCount;
                starOffsets[i] = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * starRadius;
            }

            var effectsToUse = Mathf.Min(EffectsPerChest, starOffsets.Length);
            if (_activeEffectInstances.TryGetValue(chestInstanceID, out var effectInstances) &&
                effectInstances != null && effectInstances.Count == effectsToUse)
            {
                for (var i = 0; i < effectsToUse; i++)
                {
                    var inst = effectInstances[i];
                    var offset = starOffsets[i];
                    var vfxRotation = Quaternion.LookRotation(offset.normalized, Vector3.up);
                    inst.Obj.transform.position = position + offset;
                    inst.Obj.transform.rotation = vfxRotation;
                }

                return;
            }

            if (effectInstances != null)
            {
                foreach (var inst in effectInstances)
                    if (inst.Obj != null)
                        Object.Destroy(inst.Obj);
                _activeEffectInstances.Remove(chestInstanceID);
            }

            GameObject prefab = null;
            if (ZNetScene.instance != null) prefab = ZNetScene.instance.GetPrefab(_prefabName);
            if (prefab == null) prefab = PrefabManager.Cache.GetPrefab<GameObject>(_prefabName);
            if (prefab == null) return;
            var newInstances = new List<EffectInstance>();
            for (var i = 0; i < effectsToUse; i++)
            {
                var offset = starOffsets[i];
                var vfxRotation = Quaternion.LookRotation(offset.normalized, Vector3.up);
                var obj = Object.Instantiate(prefab, position + offset, vfxRotation);
                var ps = obj.GetComponentInChildren<ParticleSystem>();
                if (ps != null)
                {
                    var psr = ps.GetComponent<ParticleSystemRenderer>();
                    if (psr != null) psr.renderMode = ParticleSystemRenderMode.Billboard;
                }

                newInstances.Add(new EffectInstance { Obj = obj, Offset = offset });
            }

            _activeEffectInstances[chestInstanceID] = newInstances;
        }

        // Allow showing/clearing effect for any target by unique ID
        public void ShowEffectForTarget(Vector3 position, Quaternion rotation, int uniqueId)
        {
            RunEffect(position, rotation, uniqueId);
        }

        public void ClearEffectForTarget(int uniqueId)
        {
            if (_activeEffectInstances.TryGetValue(uniqueId, out var effectInstances))
            {
                foreach (var inst in effectInstances)
                    if (inst.Obj != null)
                        Object.Destroy(inst.Obj);
                _activeEffectInstances.Remove(uniqueId);
            }
        }

        public void PurgeInvalid(HashSet<int> validInstanceIds)
        {
            var toRemove = _activeEffectInstances.Keys.Where(id => !validInstanceIds.Contains(id)).ToList();
            foreach (var id in toRemove)
            {
                foreach (var inst in _activeEffectInstances[id])
                    if (inst.Obj != null)
                        Object.Destroy(inst.Obj);
                _activeEffectInstances.Remove(id);
            }
        }

        private class EffectInstance
        {
            public GameObject Obj;
            public Vector3 Offset;
        }
    }
}

