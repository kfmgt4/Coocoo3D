﻿using Coocoo3D.Base;
using Coocoo3D.Components;
using Coocoo3D.Present;
using Coocoo3D.Utility;
using DefaultEcs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Core
{
    public class _physicsObjects
    {
        public List<Physics3DRigidBody> rigidbodies = new();
        public List<Physics3DJoint> joints = new();
    }
    public class PhysicsSystem
    {
        public World world;
        public Scene scene;
        EntitySet set;

        public double deltaTime;

        public bool resetPhysics;

        public Physics3DScene physics3DScene = new();

        public Dictionary<MMDRendererComponent, _physicsObjects> physicsObjects = new();

        List<MMDRendererComponent> rendererComponents = new();

        public void Initialize()
        {
            physics3DScene.Initialize();
            physics3DScene.SetGravitation(new Vector3(0, -9.801f, 0));
            world.SubscribeComponentAdded<MMDRendererComponent>(OnAdd);
            world.SubscribeComponentRemoved<MMDRendererComponent>(OnRemove);
            world.SubscribeComponentChanged<Transform>(OnChange);
            set = world.GetEntities().With<MMDRendererComponent>().AsSet();
        }

        public void OnAdd(in Entity entity, in MMDRendererComponent component)
        {
            component.SetTransform(entity.Get<Transform>());
            physicsObjects[component] = AddPhysics(component);
        }

        public void OnRemove(in Entity entity, in MMDRendererComponent component)
        {
            if (physicsObjects.Remove(component, out var phyObj))
                RemovePhysics(phyObj);
        }

        public void OnChange(in Entity entity, in Transform oldValue, in Transform newValue)
        {
            if (entity.Has<MMDRendererComponent>())
            {
                var renderer = entity.Get<MMDRendererComponent>();
                renderer.SetTransform(newValue);

                var phyObj = GetOrCreatePhysics(renderer);
                TransformToNew(renderer, phyObj.rigidbodies);
                resetPhysics = true;
            }
        }

        public void Update()
        {
            foreach (var gameObject in set.GetEntities())
            {
                var render = gameObject.Get<MMDRendererComponent>();
                rendererComponents.Add(render);
            }

            if (resetPhysics)
            {
                _ResetPhysics(rendererComponents);
                BoneUpdate((float)deltaTime, rendererComponents);
                _ResetPhysics(rendererComponents);
            }

            BoneUpdate((float)deltaTime, rendererComponents);
            rendererComponents.Clear();
            resetPhysics = false;
        }

        void _ResetPhysics(IReadOnlyList<MMDRendererComponent> renderers)
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i];
                var phyO = GetOrCreatePhysics(r);

                r.UpdateAllMatrix();
                for (int j = 0; j < r.rigidBodyDescs.Count; j++)
                {
                    var desc = r.rigidBodyDescs[j];
                    if (desc.Type == 0) continue;
                    int index = desc.AssociatedBoneIndex;
                    if (index == -1) continue;
                    var mat1 = r.bones[index].GeneratedTransform * r.LocalToWorld;
                    Matrix4x4.Decompose(mat1, out _, out var rot, out _);
                    physics3DScene.ResetRigidBody(phyO.rigidbodies[j], Vector3.Transform(desc.Position, mat1), rot * desc.Rotation);
                }
            }
            physics3DScene.Simulation(1 / 60.0);
        }

        _physicsObjects GetOrCreatePhysics(MMDRendererComponent r)
        {
            if (!physicsObjects.TryGetValue(r, out var _PhysicsObjects))
            {
                _PhysicsObjects = AddPhysics(r);
                physicsObjects[r] = _PhysicsObjects;
            }
            return _PhysicsObjects;
        }

        _physicsObjects AddPhysics(MMDRendererComponent r)
        {
            var _physicsObjects = new _physicsObjects();
            var rigidbodies = _physicsObjects.rigidbodies;
            var joints = _physicsObjects.joints;
            for (int j = 0; j < r.rigidBodyDescs.Count; j++)
            {
                rigidbodies.Add(new Physics3DRigidBody());
                var desc = r.rigidBodyDescs[j];
                physics3DScene.AddRigidBody(rigidbodies[j], desc);
            }
            for (int j = 0; j < r.jointDescs.Count; j++)
            {
                joints.Add(new Physics3DJoint());
                var desc = r.jointDescs[j];
                physics3DScene.AddJoint(joints[j], rigidbodies[desc.AssociatedRigidBodyIndex1], rigidbodies[desc.AssociatedRigidBodyIndex2], desc);
            }
            return _physicsObjects;
        }
        void RemovePhysics(_physicsObjects _physicsObjects)
        {
            var rigidbodies = _physicsObjects.rigidbodies;
            var joints = _physicsObjects.joints;
            for (int j = 0; j < rigidbodies.Count; j++)
            {
                physics3DScene.RemoveRigidBody(rigidbodies[j]);
            }
            for (int j = 0; j < joints.Count; j++)
            {
                physics3DScene.RemoveJoint(joints[j]);
            }
            rigidbodies.Clear();
            joints.Clear();
        }
        void PrePhysicsSync(MMDRendererComponent r, IReadOnlyList<Physics3DRigidBody> rigidbodies)
        {
            for (int i = 0; i < r.rigidBodyDescs.Count; i++)
            {
                var desc = r.rigidBodyDescs[i];
                if (desc.Type != 0) continue;
                int index = desc.AssociatedBoneIndex;
                if (index == -1)
                    continue;

                Matrix4x4 matrix = MatrixExt.Transform(desc.Position, desc.Rotation) * r.bones[index].GeneratedTransform * r.LocalToWorld;
                physics3DScene.MoveRigidBody(rigidbodies[i], matrix);
            }
        }

        void PhysicsSyncBack(MMDRendererComponent r, IReadOnlyList<Physics3DRigidBody> rigidbodies)
        {
            for (int i = 0; i < r.rigidBodyDescs.Count; i++)
            {
                var desc = r.rigidBodyDescs[i];
                if (desc.Type == 0) continue;
                int index = desc.AssociatedBoneIndex;
                if (index == -1) continue;
                r.bones[index]._generatedTransform = MatrixExt.InverseTransform(desc.Position, desc.Rotation) *
                    rigidbodies[i].GetTransform() * r.WorldToLocal;
                Matrix4x4 invParentMatrix;
                if (r.bones[index].ParentIndex >= 0)
                    Matrix4x4.Invert(r.bones[r.bones[index].ParentIndex]._generatedTransform, out invParentMatrix);
                else
                    invParentMatrix = Matrix4x4.Identity;

                var localMatrix = invParentMatrix * r.bones[index]._generatedTransform;
                Matrix4x4.Decompose(localMatrix, out var scale, out var rotation, out var translation);
                r.bones[index].translation = translation;
                r.bones[index].rotation = rotation;
            }
            r.UpdateMatrices(r.PhysicsNeedUpdateMatrixIndexs);

            r.UpdateAppendBones();
        }

        void BoneUpdate(float deltaTime, IReadOnlyList<MMDRendererComponent> renderers)
        {
            float t1 = Math.Clamp(deltaTime, -0.17f, 0.17f);
            for (int i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i];
                var _PhysicsObjects = GetOrCreatePhysics(r);
                PrePhysicsSync(r, _PhysicsObjects.rigidbodies);
            }
            physics3DScene.Simulation(t1 >= 0 ? t1 : -t1);
            for (int i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i];
                physicsObjects.TryGetValue(r, out var _PhysicsObjects);
                PhysicsSyncBack(r, _PhysicsObjects.rigidbodies);
            }
        }

        void TransformToNew(MMDRendererComponent r, IReadOnlyList<Physics3DRigidBody> rigidbodies)
        {
            for (int i = 0; i < r.rigidBodyDescs.Count; i++)
            {
                var desc = r.rigidBodyDescs[i];
                if (desc.Type != RigidBodyType.Kinematic) continue;
                int index = desc.AssociatedBoneIndex;
                var bone = r.bones[index];
                Matrix4x4 matrix = MatrixExt.Transform(desc.Position, desc.Rotation) * bone.GeneratedTransform * r.LocalToWorld;
                physics3DScene.MoveRigidBody(rigidbodies[i], matrix);
            }
        }
    }
}
