﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Components
{
    public class AnimationStateComponent
    {
        public string motionPath = "";
        public bool LockMotion;
        public WeightGroup Weights = new();

        public Dictionary<string, int> stringToMorphIndex = new();
        public List<(Vector3, Quaternion)> cachedBoneKeyFrames = new();

        public float timeOffset;

        public AnimationStateComponent GetClone()
        {
            AnimationStateComponent newComponent = (AnimationStateComponent)MemberwiseClone();
            newComponent.cachedBoneKeyFrames = new(cachedBoneKeyFrames);
            newComponent.stringToMorphIndex = new(stringToMorphIndex);
            newComponent.Weights = Weights.GetClone();

            return newComponent;
        }

        void SetPose(MMDMotion motionComponent, float time)
        {
            foreach (var pair in stringToMorphIndex)
            {
                Weights.Origin[pair.Value] = motionComponent.GetMorphWeight(pair.Key, time);
            }
        }
        void SetPoseDefault()
        {
            foreach (var pair in stringToMorphIndex)
            {
                Weights.Origin[pair.Value] = 0;
            }
        }

        void ComputeWeight(List<MorphDesc> morphs)
        {
            for (int i = 0; i < morphs.Count; i++)
            {
                Weights.Computed[i] = 0;
            }
            for (int i = 0; i < morphs.Count; i++)
            {
                MorphDesc morph = morphs[i];
                if (morph.Type == MorphType.Group)
                    ComputeWeightGroup(morphs, morph, Weights.Origin[i], Weights.Computed);
                else
                    Weights.Computed[i] += Weights.Origin[i];
            }
        }
        void ComputeWeightGroup(IReadOnlyList<MorphDesc> morphs, MorphDesc morph, float rate, float[] computedWeights)
        {
            for (int i = 0; i < morph.SubMorphs.Length; i++)
            {
                MorphSubMorphDesc subMorphStruct = morph.SubMorphs[i];
                MorphDesc subMorph = morphs[subMorphStruct.GroupIndex];
                if (subMorph.Type == MorphType.Group)
                    ComputeWeightGroup(morphs, subMorph, rate * subMorphStruct.Rate, computedWeights);
                else
                    computedWeights[subMorphStruct.GroupIndex] += rate * subMorphStruct.Rate;
            }
        }


        void SetBonePoseMotion(List<BoneEntity> bones, float time, MMDMotion motion)
        {
            foreach (var bone in bones)
            {
                var keyframe = motion.GetBoneMotion(bone.Name, time);
                bone.rotation = keyframe.Item2;
                bone.translation = keyframe.Item1;
                cachedBoneKeyFrames[bone.index] = keyframe;
            }
        }
        void SetBonePoseDefault(List<BoneEntity> bones)
        {
            foreach (var bone in bones)
            {
                var keyframe = new ValueTuple<Vector3, Quaternion>(Vector3.Zero, Quaternion.Identity);
                bone.rotation = keyframe.Item2;
                bone.translation = keyframe.Item1;
                cachedBoneKeyFrames[bone.index] = keyframe;
            }
        }
        void SetPoseNoMotion(List<BoneEntity> bones)
        {
            for (int i = 0; i < bones.Count; i++)
            {
                var keyframe = cachedBoneKeyFrames[i];
                bones[i].rotation = keyframe.Item2;
                bones[i].translation = keyframe.Item1;
            }
        }

        public void ComputeMotion(float time, MMDMotion motion, List<MorphDesc> morphs, List<BoneEntity> bones)
        {
            time += timeOffset;
            if (!LockMotion)
            {
                if (motion != null)
                    SetPose(motion, time);
                else
                    SetPoseDefault();
            }

            ComputeWeight(morphs);

            if (!LockMotion)
            {
                if (motion != null)
                    SetBonePoseMotion(bones, time, motion);
                else
                    SetBonePoseDefault(bones);
            }
            else
            {
                SetPoseNoMotion(bones);
            }
        }
    }

    public class WeightGroup
    {
        public float[] Origin;
        public float[] Computed;
        public void Load(int count)
        {
            Origin = new float[count];
            Computed = new float[count];
        }

        public WeightGroup GetClone()
        {
            var clone = (WeightGroup)MemberwiseClone();
            clone.Origin = new float[Origin.Length];
            clone.Computed = new float[Computed.Length];
            Array.Copy(Origin, clone.Origin, Origin.Length);
            Array.Copy(Computed, clone.Computed, Computed.Length);
            return clone;
        }
    }

    public enum MorphCategory
    {
        System = 0,
        Eyebrow = 1,
        Eye = 2,
        Mouth = 3,
        Other = 4,
    };
    public enum MorphMaterialMethon
    {
        Mul = 0,
        Add = 1,
    };

    public struct MorphSubMorphDesc
    {
        public int GroupIndex;
        public float Rate;
        public override string ToString()
        {
            return string.Format("{0},{1}", GroupIndex, Rate);
        }
    }
    public struct MorphMaterialDesc
    {
        public int MaterialIndex;
        public MorphMaterialMethon MorphMethon;
        public Vector4 Diffuse;
        public Vector4 Specular;
        public Vector3 Ambient;
        public Vector4 EdgeColor;
        public float EdgeSize;
        public Vector4 Texture;
        public Vector4 SubTexture;
        public Vector4 ToonTexture;
    }
    public struct MorphUVDesc
    {
        public int VertexIndex;
        public Vector4 Offset;
    }

    public class MorphDesc
    {
        public string Name;
        public string NameEN;
        public MorphCategory Category;
        public MorphType Type;

        public MorphSubMorphDesc[] SubMorphs;
        public MorphVertexDesc[] MorphVertexs;
        public MorphBoneDesc[] MorphBones;
        public MorphUVDesc[] MorphUVs;
        public MorphMaterialDesc[] MorphMaterials;

        public override string ToString()
        {
            return string.Format("{0}", Name);
        }
    }
}