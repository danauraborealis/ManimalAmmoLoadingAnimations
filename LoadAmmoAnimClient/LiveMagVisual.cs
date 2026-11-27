using System;
using System.Collections.Generic;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.AssetsManager;
using EFT.InventoryLogic;
using HarmonyLib;
using Manimal.LoadAmmoAnim.CustomEFTData;
using UnityEngine;

namespace Manimal.LoadAmmoAnim
{
    // where the game's own prefabs sit on an animation set's bones. measured by fitting the real
    // prefabs onto the meshes the clips were authored against, so any magazine or cartridge that
    // shares the prefab's frame drops in with no per-item work.
    internal sealed partial class MagAnimFamily
    {
        public string Key = "";
        // appended to the animator state names: each family has its own copy of the mag states
        public string StateSuffix = "";
        // where the cartridge seats in the magazine the clips were made with, magazine bone space
        public Vector3 SeatPos;
        public Quaternion SeatRot;
        // how far the baked top round travels when its pushed down a slot
        public float ShiftTravel;

        // the same for every family: the clips are all authored with the prefabs hung on the bones this way
        public static readonly Vector3 MagPos = new Vector3(-0.00016f, 0.08828f, 0.03361f);
        public static readonly Quaternion MagRot = new Quaternion(0.02904f, 0f, 0f, 0.99958f);
        public static readonly Vector3 AmmoPos = new Vector3(0f, 0.0243f, 0f);
        public static readonly Quaternion AmmoRot = new Quaternion(0.70711f, 0f, 0f, 0.70711f);
    }

    // shows the real magazine being loaded, the rounds inside it and the real cartridge in the hand,
    // all pulled from the game's pools at runtime instead of meshes baked into the bundle.
    internal sealed class LiveMagVisual : MonoBehaviour
    {
        private const string LoopStateA = "USE S 0";
        private const string LoopStateB = "USE S 1";
        // the round in the hand is steered onto this magazine's own top slot inside this range of the seat
        private const float SteerNear = 0.012f;
        private const float SteerFar = 0.035f;
        // past this the magazine itself is moved onto the reference seat
        private const float SeatAlignDistance = 0.003f;
        private const float SeatAlignAngle = 5f;

        private static readonly FieldInfo VisualField =
            AccessTools.Field(typeof(MagazineInHandsVisualController), "_magazineInHandsVisual");
        private static readonly string[] BakedRounds = { "bullet1_MESH", "bullet2_MESH", "bullet3_MESH" };

        private MagAnimFamily _family;
        private Magazine _mag;
        private Ammo _sourceAmmo;
        private Animator _animator;
        private Transform _magBone;
        private Transform _bullet1Bone;
        private Transform _bullet3Bone;
        private string _loopA;
        private string _loopB;
        private bool _steer;
        private Vector3 _steerPos;
        private Quaternion _steerRot;

        private GameObject _magObject;
        private GameObject _looseRound;
        private MagazineInHandsVisualController _magController;
        private SpringMagazineVisual _spring;

        // slots top first, with the pose each one was authored at
        private AmmoActivenessData[] _slots;
        private Vector3[] _slotPos;
        private Quaternion[] _slotRot;

        private readonly List<Renderer> _hiddenBaked = new List<Renderer>();
        private readonly List<GameObject> _extraSlots = new List<GameObject>();
        private MeshFilter _swapFilter;
        private Mesh _swapOriginal;

        private int _baseCount;
        private int _seats;
        private int _shown = -1;
        private bool _seatedThisLoop;
        private bool _looseWasVisible;
        private int _lastState;
        private float _lastTime;
        private Vector3 _bullet1Rest;
        private bool _haveBullet1Rest;
        private float _lastPhi = -1f;
        private bool _cleaned;

        // every round the game has loaded has been shown going in
        public bool CaughtUp => _mag == null || _baseCount + _seats >= _mag.Count;

        // the loop is at its first pose, which is also the put-away clip's
        public bool AtLoopStart { get; private set; } = true;

        public static LiveMagVisual TryAttach(
            GameObject prefabRoot, Player player, Magazine mag, Ammo sourceAmmo, Animator animator, MagAnimFamily family)
        {
            if (!prefabRoot || mag == null || !animator || !Singleton<ObjectsFactory>.Instantiated) return null;

            Transform magBone = null, bullet1 = null, bullet3 = null;
            foreach (var t in prefabRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Magazine") magBone = t;
                else if (t.name == "Bullet1") bullet1 = t;
                else if (t.name == "Bullet3") bullet3 = t;
            }
            if (!magBone || !bullet1 || !bullet3) return null;

            var live = prefabRoot.AddComponent<LiveMagVisual>();
            try
            {
                live._family = family;
                live._mag = mag;
                live._sourceAmmo = sourceAmmo;
                live._animator = animator;
                live._magBone = magBone;
                live._bullet1Bone = bullet1;
                live._bullet3Bone = bullet3;
                live._loopA = LoopStateA + family.StateSuffix;
                live._loopB = LoopStateB + family.StateSuffix;
                live.Spawn(prefabRoot, player, magBone);
                return live;
            }
            catch (Exception ex)
            {
                Plugin.LogSource?.LogWarning(
                    $"[LoadAmmoAnim] real magazine model unavailable, using the baked mesh: {ex.GetType().Name}: {ex.Message}");
                live.Detach();
                return null;
            }
        }

        private void Spawn(GameObject prefabRoot, Player player, Transform magBone)
        {
            var factory = Singleton<ObjectsFactory>.Instance;

            _magObject = factory.CreateItem(_mag, Player.GetVisibleToCamera(player), player, false);
            if (!_magObject) throw new InvalidOperationException("CreateItem returned nothing for the magazine");

            // same layer the bundle's own meshes were given, cartridges stay on theirs like on a weapon
            int layer = prefabRoot.layer;
            foreach (var r in prefabRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (MagAnimLookup.AllMeshNames.Contains(r.gameObject.name)) { layer = r.gameObject.layer; break; }
            }

            var mt = _magObject.transform;
            mt.SetParent(magBone, false);
            mt.localPosition = MagAnimFamily.MagPos;
            mt.localRotation = MagAnimFamily.MagRot;
            mt.localScale = Vector3.one;
            TransformTools.SetLayersRecursively(_magObject, layer, "Shells");
            _magObject.SetActive(true);

            _magController = _magObject.GetComponent<MagazineInHandsVisualController>();
            if (_magController) _spring = VisualField?.GetValue(_magController) as SpringMagazineVisual;
            if (_spring != null && _spring._cartridgesTransforms != null && _spring._cartridgesTransforms.Length > 0)
            {
                // the game only ever takes rounds away as the count drops. loading needs them added, and
                // one shown a moment before the count moves, so the slots are driven from here instead.
                _magController.enabled = false;
                _slots = (AmmoActivenessData[])_spring._cartridgesTransforms.Clone();
                Array.Sort(_slots, (a, b) => a.Id.CompareTo(b.Id));
                ApplySwap(prefabRoot);
                _slotPos = new Vector3[_slots.Length];
                _slotRot = new Quaternion[_slots.Length];
                for (int i = 0; i < _slots.Length; i++)
                {
                    _slotPos[i] = _slots[i].Transform.localPosition;
                    _slotRot[i] = _slots[i].Transform.localRotation;
                }

                // top slots differ between magazines of one family, some even sit on the other side of
                // the stack. the clip seats the round in the reference magazine's: note how far off this one is.
                var top = _slots[0].Transform;
                var seatPos = magBone.InverseTransformPoint(top.position);
                var seatRot = Quaternion.Inverse(magBone.rotation) * top.rotation * Quaternion.Euler(90f, 0f, 0f);
                _steerPos = seatPos - _family.SeatPos;
                _steerRot = seatRot * Quaternion.Inverse(_family.SeatRot);

                // a magazine from another weapon hangs off a different mag-well frame, so its lips can sit
                // centimetres from where the hands work. stand it with its own top slot on the reference's.
                // which side of the stack the top round is on stays the round's business, so the body
                // keeps centred in the fist.
                var axis = _family.SeatRot * Vector3.forward;
                var lateral = Vector3.zero;
                var below = _slots.Length > 2 ? _slots[2].Transform : (_slots.Length > 1 ? _slots[1].Transform : null);
                if (below)
                {
                    var run = magBone.InverseTransformPoint(below.position) - seatPos;
                    run -= axis * Vector3.Dot(run, axis);
                    if (run.sqrMagnitude > 1e-10f) lateral = Vector3.Cross(axis, run.normalized);
                }
                float side = Vector3.Dot(_steerPos, lateral);
                if ((_steerPos - lateral * side).magnitude > SeatAlignDistance
                    || Quaternion.Angle(Quaternion.identity, _steerRot) > SeatAlignAngle)
                {
                    var fix = _family.SeatRot * Quaternion.Inverse(seatRot);
                    mt.localRotation = fix * mt.localRotation;
                    mt.localPosition = _family.SeatPos + lateral * side + fix * (mt.localPosition - seatPos);
                    seatPos = magBone.InverseTransformPoint(top.position);
                    seatRot = Quaternion.Inverse(magBone.rotation) * top.rotation * Quaternion.Euler(90f, 0f, 0f);
                    _steerPos = seatPos - _family.SeatPos;
                    _steerRot = seatRot * Quaternion.Inverse(_family.SeatRot);
                }
                _steer = _steerPos.magnitude > 0.0003f || Quaternion.Angle(Quaternion.identity, _steerRot) > 0.5f;
            }
            else
            {
                _spring = null;
            }

            if (_sourceAmmo != null)
            {
                _looseRound = factory.CreateItem(_sourceAmmo, false);
                if (_looseRound)
                {
                    var apo = _looseRound.GetComponent<AmmoPoolObject>();
                    if (apo) apo.SetUsed(false);
                    // hangs off the magazine bone and is posed from the round bone every frame, so it can be steered
                    var lt = _looseRound.transform;
                    lt.SetParent(magBone, false);
                    lt.localScale = Vector3.one;
                    PoseLooseRound();
                }
            }

            foreach (var r in prefabRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.enabled || Array.IndexOf(BakedRounds, r.gameObject.name) < 0) continue;
                r.enabled = false;
                _hiddenBaked.Add(r);
            }

            _baseCount = _mag.Count;
            Refill(_mag.Count);
        }

        private void LateUpdate()
        {
            if (_cleaned || !_magObject || !_animator || _mag == null) return;

            var info = _animator.GetCurrentAnimatorStateInfo(LoadAmmoBundleController.HandsLayer);
            bool inLoop = info.IsName(_loopA) || info.IsName(_loopB);
            float t = info.normalizedTime % 1f;

            // the two loop states hand over to each other every round
            if (info.shortNameHash != _lastState || t < _lastTime - 0.5f) _seatedThisLoop = false;
            _lastState = info.shortNameHash;
            _lastTime = t;

            bool looseVisible = _bullet3Bone.localScale.x > 0.99f;
            if (inLoop && !_seatedThisLoop && _looseWasVisible && !looseVisible && t > 0.5f)
            {
                // the clip hides the round in the fingers on the frame it is home
                _seats++;
                _seatedThisLoop = true;
            }
            _looseWasVisible = looseVisible;
            AtLoopStart = !inLoop || (!_seatedThisLoop && t < 0.1f);

            int real = _mag.Count;
            int anim = _baseCount + _seats;
            if (Math.Abs(anim - real) > 1)
            {
                // drifted more than a round apart: fall back in step with the game
                _seats = real - _baseCount;
                anim = real;
            }
            int want = Mathf.Clamp(inLoop ? anim : real, 0, _mag.MaxCount);
            if (want != _shown) Refill(want);

            if (_looseRound)
            {
                // shown even when the mag is full: a hand pinching nothing reads as a missing round
                bool show = looseVisible;
                if (_looseRound.activeSelf != show) _looseRound.SetActive(show);
                if (show) PoseLooseRound();
            }

            if (inLoop && !_seatedThisLoop && t < 0.15f)
            {
                _bullet1Rest = _bullet1Bone.localPosition;
                _haveBullet1Rest = true;
            }
            float phi = 0f;
            if (inLoop && !_seatedThisLoop && _haveBullet1Rest)
                phi = Mathf.Clamp((_bullet1Bone.localPosition - _bullet1Rest).magnitude / _family.ShiftTravel, 0f, 3f);
            ShiftSlots(phi);
        }

        // some magazines have dummy rounds modelled into the mesh right where the real ones go (c-mag: seven,
        // against three real slots), so nothing can move them. the bundle carries that mesh with the dummies
        // cut out under swap_<template id>, with slots for real rounds in their place.
        private void ApplySwap(GameObject prefabRoot)
        {
            Transform swap = null;
            string template = _mag.TemplateId;
            string wanted = "swap_" + template;
            foreach (var t in prefabRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != wanted) continue;
                swap = t;
                break;
            }
            if (!swap) return;
            var source = swap.GetComponent<MeshFilter>();
            if (!source || !source.sharedMesh) return;

            // same vertex buffer or its not the mesh this was cut from
            foreach (var mf in _magObject.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh || mf.sharedMesh.vertexCount != source.sharedMesh.vertexCount) continue;
                _swapFilter = mf;
                _swapOriginal = mf.sharedMesh;
                mf.sharedMesh = source.sharedMesh;
                break;
            }
            if (!_swapFilter) return;

            var parent = _slots[0].Transform.parent;
            var all = new List<AmmoActivenessData>(_slots);
            for (int i = 0; i < swap.childCount; i++)
            {
                var c = swap.GetChild(i);
                if (!c.name.StartsWith("slot_") || !int.TryParse(c.name.Substring(5), out int id)) continue;
                // not named patron_: the game collects those as its own
                var go = new GameObject("lma_slot_" + id);
                go.layer = parent.gameObject.layer;
                go.transform.SetParent(parent, false);
                go.transform.localPosition = c.localPosition;
                go.transform.localRotation = c.localRotation;
                _extraSlots.Add(go);
                all.Add(new AmmoActivenessData(go.transform, id));
            }
            _slots = all.ToArray();
            Array.Sort(_slots, (a, b) => a.Id.CompareTo(b.Id));
        }

        // puts the cartridge where the clip has the round bone, bent toward this magazine's top slot on the way in
        private void PoseLooseRound()
        {
            var rot = Quaternion.Inverse(_magBone.rotation) * _bullet3Bone.rotation * MagAnimFamily.AmmoRot;
            var pos = _magBone.InverseTransformPoint(_bullet3Bone.position + _bullet3Bone.rotation * MagAnimFamily.AmmoPos);
            if (_steer)
            {
                float w = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SteerNear, SteerFar, (pos - _family.SeatPos).magnitude));
                var turn = Quaternion.Slerp(Quaternion.identity, _steerRot, w);
                pos = turn * (pos - _family.SeatPos) + _family.SeatPos + _steerPos * w;
                rot = turn * rot;
            }
            var lt = _looseRound.transform;
            lt.localPosition = pos;
            lt.localRotation = rot;
        }

        // the stack moving down a slot as the new round is pressed in, read off the baked top round
        private void ShiftSlots(float phi)
        {
            if (_slots == null || _slots.Length < 2 || Mathf.Approximately(phi, _lastPhi)) return;
            _lastPhi = phi;
            // drum towers get the round pressed a full pitch past its seat, so a shift can pass one slot
            int whole = Mathf.FloorToInt(phi);
            float part = phi - whole;
            for (int i = 0; i < _slots.Length; i++)
            {
                SlotPose(i + whole, out var fromPos, out var fromRot);
                SlotPose(i + whole + 1, out var toPos, out var toRot);
                var st = _slots[i].Transform;
                st.localPosition = Vector3.LerpUnclamped(fromPos, toPos, part);
                st.localRotation = Quaternion.SlerpUnclamped(fromRot, toRot, part);
            }
        }

        // rest pose of a slot by index. rounds pushed past the last one wait there — carrying on the way
        // the stack was heading walks a double stack out through the side wall
        private void SlotPose(int index, out Vector3 pos, out Quaternion rot)
        {
            int i = Mathf.Min(index, _slots.Length - 1);
            pos = _slotPos[i];
            rot = _slotRot[i];
        }

        // same fill the game does on spawn, for a count of our choosing. rounds past the real count are
        // the ones on their way in, drawn as the ammo being loaded.
        private void Refill(int count)
        {
            _shown = count;
            if (_spring == null || _slots == null) return;

            _spring.ReturnVisualPartsToPool();
            var factory = Singleton<ObjectsFactory>.Instance;
            int real = _mag.Count;
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot.Id > count)
                {
                    slot.SetActive(false);
                    continue;
                }
                slot.SetActive(true);

                int position = count - 1 - i;
                Ammo ammo = null;
                if (position >= 0 && position < real) ammo = _mag.GetBulletAtPosition(position);
                if (ammo == null) ammo = _sourceAmmo;
                if (ammo == null) continue;

                var go = factory.CreateItem(ammo, false);
                if (!go) continue;
                var apo = go.GetComponent<AmmoPoolObject>();
                // on the game's own list, so its pool return takes them back
                _spring._ammoPoolObjects.Add(apo);
                if (apo) apo.SetUsed(false);
                var gt = go.transform;
                gt.SetParent(slot.Transform, false);
                gt.localPosition = Vector3.zero;
                gt.localRotation = Quaternion.Euler(90f, 0f, 0f);
                go.SetActive(true);
            }
        }

        // hands everything back to the pools as the game left it. these objects go on to be real
        // weapon magazines, so the slot poses and the controller have to be put back.
        public void Detach()
        {
            if (_cleaned) return;
            _cleaned = true;
            try
            {
                if (_slots != null)
                {
                    for (int i = 0; i < _slots.Length; i++)
                    {
                        var st = _slots[i].Transform;
                        if (!st) continue;
                        st.localPosition = _slotPos[i];
                        st.localRotation = _slotRot[i];
                    }
                }
                if (_magController) _magController.enabled = true;

                if (_swapFilter) _swapFilter.sharedMesh = _swapOriginal;
                _swapFilter = null;
                _swapOriginal = null;
                // our own slots are not the pool's: the rounds on them go back first, then the slots are dropped
                if (_extraSlots.Count > 0 && _spring != null) _spring.ReturnVisualPartsToPool();
                for (int i = 0; i < _extraSlots.Count; i++)
                {
                    var extra = _extraSlots[i];
                    if (!extra) continue;
                    for (int c = extra.transform.childCount - 1; c >= 0; c--)
                        Return(extra.transform.GetChild(c).gameObject);
                    extra.transform.SetParent(null, false);
                    Destroy(extra);
                }
                _extraSlots.Clear();

                Return(_looseRound);
                Return(_magObject);

                for (int i = 0; i < _hiddenBaked.Count; i++)
                    if (_hiddenBaked[i]) _hiddenBaked[i].enabled = true;
                _hiddenBaked.Clear();
            }
            catch (Exception ex)
            {
                Plugin.LogSource?.LogError($"[LoadAmmoAnim] LiveMagVisual.Detach: {ex.GetType().Name}: {ex.Message}");
            }
            _looseRound = null;
            _magObject = null;
            _spring = null;
            _slots = null;
            Destroy(this);
        }

        // when the bundle object itself is pooled first, the game has already taken its pooled children back
        private static void Return(GameObject go)
        {
            if (!go) return;
            var apo = go.GetComponent<AssetPoolObject>();
            if (apo && apo.IsInPool) return;
            AssetPoolObject.ReturnToPool(go, true);
        }

        // the bundle object went back to its pool without the driver getting to us first
        private void OnDisable() => Detach();
    }
}
