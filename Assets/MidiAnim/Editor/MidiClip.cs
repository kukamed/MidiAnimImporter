using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace MidiAnim
{
    class MidiClip
    {
        float _bpm;
        float _deltaTime;

        AnimationCurve _beatCount = new AnimationCurve();
        AnimationCurve _beatClock = new AnimationCurve();

        AnimationCurve _barCount = new AnimationCurve();
        AnimationCurve _barClock = new AnimationCurve();

        AnimationCurve[] _noteCurves = new AnimationCurve[128];
        AnimationCurve[] _ccCurves = new AnimationCurve[128];

        int _beat = -1;

        public MidiClip(float bpm, float deltaTime)
        {
            _bpm = bpm;
            _deltaTime = deltaTime;
        }

        public void WriteBeat(float time)
        {
            var beatAtTime = (int)(_bpm * time / 60);

            // Do nothing if it's still in the same beat.
            if (beatAtTime == _beat) return;

            // Update the beat number.
            _beat = beatAtTime;

            // Beat count
            _beatCount.AddKey(time, _beat);

            // Beat clock curve
            if (_beat > 0) _beatClock.AddKey(time - _deltaTime, 1);
            _beatClock.AddKey(time, 0);

            if (_beat % 4 == 0)
            {
                // Bar count
                _barCount.AddKey(time, _beat / 4);

                // Bar clock curve
                if (_beat > 0) _barClock.AddKey(time - _deltaTime, 1);
                _barClock.AddKey(time, 0);
            }
        }

        void SetNoteKey(int index, float time, float value)
        {
            if (_noteCurves[index] == null)
                _noteCurves[index] = new AnimationCurve();

            _noteCurves[index].AddKey(time, value);
        }

        void SetCCKey(int index, float time, float value)
        {
            var curve = _ccCurves[index];

            if (curve == null)
            {
                _ccCurves[index] = new AnimationCurve();
                _ccCurves[index].AddKey(time, value);
            }
            else
            {
                // Add a new key, or replace the last key if it's too close.
                var last = curve.length - 1;
                if (Mathf.Approximately(curve[last].time, time))
                    curve.MoveKey(last, new Keyframe(time, value));
                else
                    curve.AddKey(time, value);
            }
        }

        public void WriteEvents(float time, List<MidiEvent> events)
        {
            if (events == null) return;

            foreach (var e in events)
            {
                var stat = e.status & 0xf0;
                var index = e.data1;

                if (stat == 0x90) // Note on
                    SetNoteKey(index, time, (float)e.data2 / 127);
                else if (stat == 0x80) // Note off
                    SetNoteKey(index, time - _deltaTime, 0);
                else if (stat == 0xb0) // CC
                    SetCCKey(index, time, e.data2);
            }
        }

        public AnimationClip ConvertToAnimationClip()
        {
            var dest = new AnimationClip();

            ModifyTangentsForCount(_beatCount);
            ModifyTangentsForClock(_beatClock);
            ModifyTangentsForCount(_barCount);
            ModifyTangentsForClock(_barClock);

            dest.SetCurve("", typeof(MidiState), "BeatCount", _beatCount);
            dest.SetCurve("", typeof(MidiState), "BeatClock", _beatClock);
            dest.SetCurve("", typeof(MidiState), "BarCount", _barCount);
            dest.SetCurve("", typeof(MidiState), "BarClock", _barClock);

            for (var i = 0; i < _noteCurves.Length; i++)
            {
                var curve = _noteCurves[i];
                if (curve == null) continue;
                ModifyTangentsForNotes(curve);
                dest.SetCurve("", typeof(MidiState), "Note[" + i + "]", curve);
            }

            for (var i = 0; i < _ccCurves.Length; i++)
            {
                var curve = _ccCurves[i];
                if (curve == null) continue;
                ModifyTangentsForCC(curve);
                dest.SetCurve("", typeof(MidiState), "CC[" + i + "]", curve);
            }

            return dest;
        }

        #region Animation curve utilities

        static void ModifyTangentsForCount(AnimationCurve curve)
        {
            var tan = AnimationUtility.TangentMode.Constant;
            for (var i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, tan);
                AnimationUtility.SetKeyRightTangentMode(curve, i, tan);
            }
        }

        static void ModifyTangentsForClock(AnimationCurve curve)
        {
            var ctan = AnimationUtility.TangentMode.Constant;
            var ltan = AnimationUtility.TangentMode.Linear;

            for (var i = 0; i < curve.length; i++)
            {
                if (curve[i].value < 0.5f)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, ctan);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, ltan);
                }
                else
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, ltan);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, ctan);
                }
            }
        }

        static void ModifyTangentsForNotes(AnimationCurve curve)
        {
            var ctan = AnimationUtility.TangentMode.Constant;
            var ltan = AnimationUtility.TangentMode.Linear;

            for (var i = 0; i < curve.length; i++)
            {
                if (curve[i].value > 0.5f)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, ctan);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, ltan);
                }
                else
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, ltan);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, ctan);
                }
            }
        }

        static void ModifyTangentsForCC(AnimationCurve curve)
        {
            var tan = AnimationUtility.TangentMode.Constant;
            for (var i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, tan);
                AnimationUtility.SetKeyRightTangentMode(curve, i, tan);
            }
        }

        #endregion
    }
}