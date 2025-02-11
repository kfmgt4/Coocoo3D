﻿using Caprice.Display;
using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using Coocoo3D.Utility;
using DefaultEcs;
using DefaultEcs.Command;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading;

namespace Coocoo3D.UI
{
    public class UIImGui
    {
        public PlatformIO platformIO;

        public WindowSystem windowSystem;

        public RecordSystem recordSystem;

        public RenderSystem renderSystem;

        public UIRenderSystem uiRenderSystem;

        public MainCaches mainCaches;

        public GameDriverContext gameDriverContext;

        public GameDriver gameDriver;

        public Scene CurrentScene;

        public RenderPipelineContext renderPipelineContext;

        public Config config;

        public Statistics statistics;

        public EntityCommandRecorder recorder;

        public void GUI()
        {
            var io = ImGui.GetIO();
            Input();

            Vector2 mouseMoveDelta = new Vector2();
            foreach (var delta in platformIO.mouseMoveDelta)
            {
                mouseMoveDelta += delta;
            }

            var context = renderPipelineContext;
            io.DisplaySize = new Vector2(platformIO.windowSize.Item1, platformIO.windowSize.Item2);
            io.DeltaTime = (float)context.RealDeltaTime;
            Entity selectedObject = default(Entity);

            positionChange = false;
            rotationChange = false;
            scaleChange = false;
            if (CurrentScene.SelectedGameObjects.Count == 1)
            {
                foreach (var entity in CurrentScene.world)
                {
                    if (entity.GetHashCode() == CurrentScene.SelectedGameObjects[0])
                    {
                        selectedObject = entity;
                    }
                }
                ref var transform = ref selectedObject.Get<Transform>();
                position = transform.position;
                scale = transform.scale;
                if (rotationCache != transform.rotation)
                {
                    rotation = QuaternionToEularYXZ(transform.rotation);
                    rotationCache = transform.rotation;
                }
            }


            ImGui.NewFrame();

            if (demoWindowOpen)
                ImGui.ShowDemoWindow(ref demoWindowOpen);

            DockSpace();
            ImGui.SetNextWindowPos(new Vector2(0, 0), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(300, 400), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("常用"))
            {
                Common();
            }
            ImGui.End();
            ImGui.SetNextWindowSize(new Vector2(500, 300), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(300, 400), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("资源"))
            {
                var _openRequest = Resources();
                if (openRequest == null)
                    openRequest = _openRequest;
            }
            ImGui.End();
            ImGui.SetNextWindowPos(new Vector2(800, 0), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(300, 400), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("设置"))
            {
                SettingsPanel();
            }
            ImGui.End();
            ImGui.SetNextWindowSize(new Vector2(350, 300), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(750, 0), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("场景层级"))
            {
                SceneHierarchy();
            }
            ImGui.End();
            int d = 0;
            foreach (var visualChannel in windowSystem.visualChannels.Values)
            {
                ImGui.SetNextWindowSize(new Vector2(400, 400), ImGuiCond.FirstUseEver);
                ImGui.SetNextWindowPos(new Vector2(300 + d, 0), ImGuiCond.FirstUseEver);
                if (visualChannel.Name != "main")
                {
                    bool open = true;
                    if (ImGui.Begin(string.Format("场景视图 - {0}###SceneView/{0}", visualChannel.Name), ref open))
                    {
                        SceneView(visualChannel, io.MouseWheel, mouseMoveDelta);
                    }
                    if (!open)
                    {
                        windowSystem.DelayRemoveVisualChannel(visualChannel.Name);
                    }
                }
                else
                {
                    if (ImGui.Begin(string.Format("场景视图 - {0}###SceneView/{0}", visualChannel.Name)))
                    {
                        SceneView(visualChannel, io.MouseWheel, mouseMoveDelta);
                    }
                }
                ImGui.End();
                d += 50;
            }
            windowSystem.UpdateChannels();
            ImGui.SetNextWindowSize(new Vector2(300, 300), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(0, 400), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("物体"))
            {
                if (selectedObject != default(Entity))
                {
                    GameObjectPanel(selectedObject);
                }
            }
            ImGui.End();
            RenderBuffersPannel();
            Popups(selectedObject);
            ImGui.Render();
            if (selectedObject != default(Entity))
            {
                bool transformChange = rotationChange || positionChange;
                if (rotationChange)
                {
                    rotationCache = Quaternion.CreateFromYawPitchRoll(rotation.Y, rotation.X, rotation.Z);
                }
                if (transformChange)
                {
                    selectedObject.Set(new Transform(position, rotationCache, scale));
                }
            }
            platformIO.dropFile = null;
        }

        void Input()
        {
            var io = ImGui.GetIO();
            for (int i = 0; i < 256; i++)
            {
                io.KeysDown[i] = platformIO.keydown[i];
            }
            foreach (var c in platformIO.inputChars)
                io.AddInputCharacter(c);

            io.KeyCtrl = platformIO.KeyControl;
            io.KeyShift = platformIO.KeyShift;
            io.KeyAlt = platformIO.KeyAlt;
            io.KeySuper = platformIO.KeySuper;


            io.MouseWheel += Interlocked.Exchange(ref platformIO.mouseWheelV, 0);
            io.MouseWheelH += Interlocked.Exchange(ref platformIO.mouseWheelH, 0);
            #region mouse inputs
            for (int i = 0; i < 5; i++)
                io.MouseDown[i] = platformIO.mouseDown[i];
            io.MousePos = platformIO.mousePosition;
            #endregion

            #region outputs
            platformIO.WantCaptureKeyboard = io.WantCaptureKeyboard;
            platformIO.WantCaptureMouse = io.WantCaptureMouse;
            platformIO.WantSetMousePos = io.WantSetMousePos;
            platformIO.WantTextInput = io.WantTextInput;

            platformIO.setMousePos = io.MousePos;
            platformIO.requestCursor = ImGui.GetMouseCursor();
            #endregion
        }

        void Common()
        {
            var camera = windowSystem.currentChannel.camera;
            if (ImGui.TreeNode("变换"))
            {
                if (ImGui.DragFloat3("位置", ref position, 0.01f))
                {
                    positionChange = true;
                }
                Vector3 a = rotation / MathF.PI * 180;
                if (ImGui.DragFloat3("旋转", ref a))
                {
                    rotation = a * MathF.PI / 180;
                    rotationChange = true;
                }
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("相机"))
            {
                ImGui.DragFloat("距离", ref camera.Distance, 0.01f);
                ImGui.DragFloat3("焦点", ref camera.LookAtPoint, 0.05f);
                Vector3 a = camera.Angle / MathF.PI * 180;
                if (ImGui.DragFloat3("角度", ref a))
                    camera.Angle = a * MathF.PI / 180;
                float fov = camera.Fov / MathF.PI * 180;
                if (ImGui.DragFloat("FOV", ref fov, 0.5f, 0.1f, 179.9f))
                    camera.Fov = fov * MathF.PI / 180;
                ImGui.DragFloat("近裁剪", ref camera.nearClip, 0.01f, 0.01f, float.MaxValue);
                ImGui.DragFloat("远裁剪", ref camera.farClip, 1.0f, 0.01f, float.MaxValue);

                ImGui.Checkbox("使用镜头运动文件", ref camera.CameraMotionOn);
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("录制"))
            {
                if (recordSystem.ffmpegInstalled)
                {
                    ImGui.Text("已安装FFmpeg，将使用FFmpeg录制。输出文件名output.mp4");
                }
                var recordSettings = recordSystem.recordSettings;
                ImGui.DragFloat("开始时间", ref recordSettings.StartTime);
                ImGui.DragFloat("结束时间", ref recordSettings.StopTime);
                ImGui.DragInt("宽度", ref recordSettings.Width, 32, 32, 16384);
                ImGui.DragInt("高度", ref recordSettings.Height, 8, 8, 16384);
                ImGui.DragFloat("FPS", ref recordSettings.FPS, 1, 1, 1000);
                if (ImGui.Button("开始录制"))
                {
                    requestRecord = true;
                }
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("帮助"))
            {
                Help();
                ImGui.TreePop();
            }
            if (ImGui.Button("播放"))
            {
                Play();
            }
            ImGui.SameLine();
            if (ImGui.Button("暂停"))
            {
                Pause();
            }
            ImGui.SameLine();
            if (ImGui.Button("停止"))
            {
                Stop();
            }
            if (ImGui.Button("跳到最前"))
            {
                Front();
            }
            ImGui.SameLine();
            if (ImGui.Button("重置物理"))
            {
                gameDriverContext.RequireResetPhysics = true;
            }
            if (ImGui.Button("快进"))
            {
                FastForward();
            }
            ImGui.SameLine();
            if (ImGui.Button("向前5秒"))
            {
                gameDriver.ToPlayMode();
                gameDriverContext.PlayTime -= 5;
                gameDriverContext.RequireRender(true);
            }
            ImGui.Text(string.Format("Fps:{0:f1}", statistics.FramePerSecond));
            foreach (var input in mainCaches.textureDecodeHandler.inputs)
            {
                ImGui.Text(((TextureLoadTask)input).KnownFile.fullPath);
            }
        }

        void SettingsPanel()
        {
            ImGui.Checkbox("垂直同步", ref config.VSync);
            ImGui.Checkbox("节省CPU", ref config.SaveCpuPower);
            float a = (float)(1.0 / Math.Clamp(gameDriverContext.FrameInterval, 1e-4, 1));
            if (ImGui.DragFloat("帧率限制", ref a, 10, 1, 5000))
            {
                gameDriverContext.FrameInterval = Math.Clamp(1 / a, 1e-4f, 1f);
            }

            if (loadRPRequest != null)
            {
                renderSystem.LoadRenderPipelines(loadRPRequest);
                loadRPRequest = null;
            }
            var rpc = windowSystem;
            ShowParams(rpc.currentChannel.renderPipelineView, rpc.currentChannel.renderPipeline);
            int renderPipelineIndex = 0;

            var rps = renderSystem.RenderPipelineTypes;

            string[] newRPs = new string[rps.Count];
            for (int i = 0; i < rps.Count; i++)
            {
                var uiShowAttribute = rps[i].GetCustomAttribute<UIShowAttribute>(true);
                if (uiShowAttribute != null)
                    newRPs[i] = uiShowAttribute.Name;
                else
                    newRPs[i] = rps[i].ToString();
                if (rps[i] == rpc.currentChannel.renderPipeline?.GetType())
                {
                    renderPipelineIndex = i;
                }
            }

            if (ImGui.Combo("渲染管线", ref renderPipelineIndex, newRPs, rps.Count))
            {
                rpc.currentChannel.DelaySetRenderPipeline(rps[renderPipelineIndex]);
            }
            if (ImGui.Button("加载渲染管线"))
            {
                requestSelectRenderPipelines = true;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("默认的渲染管线位于软件的Samples文件夹，软件会在启动时加载这些插件。");
            }

            if (ImGui.Button("添加视口"))
            {
                int c = 1;
                while (true)
                {
                    if (!windowSystem.visualChannels.ContainsKey(c.ToString()))
                    {
                        windowSystem.DelayAddVisualChannel(c.ToString());
                        break;
                    }
                    c++;
                }
            }
            if (ImGui.Button("保存场景"))
            {
                requestSave = true;
            }
            if (ImGui.Button("重新加载纹理"))
            {
                mainCaches.ReloadTextures = true;
            }
            if (ImGui.Button("重新加载Shader"))
            {
                mainCaches.ReloadShaders = true;
            }
            ImGui.TextUnformatted("绘制三角形数：" + statistics.DrawTriangleCount); ;
        }

        void ShowParams(RenderPipelineView view, object tree1)
        {
            if (view == null)
                return;
            ImGui.Separator();
            string filter = ImFilter("查找参数", "搜索参数名称");
            var usages1 = mainCaches.GetUIUsage(tree1.GetType());
            foreach (var param in usages1)
            {
                string name = param.MemberInfo.Name;

                if (param.UIShowType != UIShowType.All && param.UIShowType != UIShowType.Global)
                    continue;
                if (!Contains(param.Name, filter) && !Contains(param.MemberInfo.Name, filter))
                    continue;

                var member = param.MemberInfo;
                object obj = member.GetValue<object>(tree1);
                var type = obj.GetType();
                if (type.IsEnum)
                {
                    if (ImGuiExt.ComboBox(param.Name, ref obj))
                    {
                        member.SetValue(tree1, obj);
                    }
                }
                else if (param.treeAttribute != null)
                {
                    if (ImGui.TreeNode(param.Name))
                    {
                        ShowParams(view, member.GetValue<object>(tree1));

                        ImGui.TreePop();
                    }
                }
                else
                {
                    ShowParam1(param, tree1, () =>
                    {
                        if (member.GetGetterType() == typeof(Coocoo3DGraphics.Texture2D))
                        {
                            view.textureReplacement.TryGetValue(name, out string rep);
                            return rep;
                        }
                        else
                            return member.GetValue<object>(tree1);
                    },
                    (object o1) =>
                    {
                        if (member.GetGetterType() == typeof(Coocoo3DGraphics.Texture2D))
                        {
                            view.SetReplacement(name, (string)o1);
                            view.InvalidDependents(name);
                        }
                        else
                        {
                            member.SetValue(tree1, o1);
                            view.InvalidDependents(name);
                        }
                    });
                }
            }
        }

        void ShowParams(UIShowType showType, RenderPipelineView view, Dictionary<string, object> parameters)
        {
            if (view == null)
                return;
            ShowParams(showType, view.renderPipeline, parameters);
        }
        void ShowParams(UIShowType showType, object renderPipeline, Dictionary<string, object> parameters)
        {
            ImGui.Separator();
            string filter = ImFilter("查找参数", "搜索参数名称");

            foreach (var param in mainCaches.GetUIUsage(renderPipeline.GetType()))
            {
                string name = param.MemberInfo.Name;

                if (param.UIShowType != UIShowType.All && (param.UIShowType & showType) == 0)
                    continue;
                if (!Contains(param.Name, filter) && !Contains(param.MemberInfo.Name, filter))
                    continue;

                var member = param.MemberInfo;
                object pipelineMemberValue = member.GetValue<object>(renderPipeline);
                var type = pipelineMemberValue.GetType();
                if (type.IsEnum)
                {
                    if (parameters.TryGetValue(name, out var parameter1))
                        pipelineMemberValue = parameter1;
                    if (pipelineMemberValue is string s && Enum.TryParse(type, s, out var enumValue))
                    {
                        pipelineMemberValue = enumValue;
                    }
                    if (pipelineMemberValue.GetType() != type)
                    {
                        pipelineMemberValue = Activator.CreateInstance(type);
                    }
                    if (ImGuiExt.ComboBox(param.Name, ref pipelineMemberValue))
                    {
                        parameters[name] = pipelineMemberValue;
                    }
                }
                else
                {
                    ShowParam1(param, renderPipeline, () =>
                    {
                        parameters.TryGetValue(name, out var parameter);
                        return parameter;
                    },
                    (object o1) => { parameters[name] = o1; },
                    true);
                }
            }
        }

        void ShowParam1(UIUsage param, object renderPipeline, Func<object> getter, Action<object> setter, bool viewOverride = false)
        {
            var member = param.MemberInfo;
            object obj = member.GetValue<object>(renderPipeline);
            var type = obj.GetType();

            string displayName = param.Name;
            string name = member.Name;

            bool propertyOverride = false;
            object parameter = getter.Invoke();
            if (parameter != null && type == parameter.GetType())
            {
                propertyOverride = viewOverride;
                obj = parameter;
            }
            if (propertyOverride)
                ImGui.PushStyleColor(ImGuiCol.Text, 0xffaaffaa);
            var sliderAttribute = param.sliderAttribute;
            var colorAttribute = param.colorAttribute;
            var dragFloatAttribute = param.dragFloatAttribute;
            var dragIntAttribute = param.dragIntAttribute;
            switch (obj)
            {
                case bool val:
                    if (ImGui.Checkbox(displayName, ref val))
                    {
                        setter.Invoke(val);
                    }
                    break;
                case float val:
                    if (sliderAttribute != null)
                    {
                        if (ImGui.SliderFloat(displayName, ref val, sliderAttribute.Min, sliderAttribute.Max))
                        {
                            setter.Invoke(val);
                        }
                    }
                    else if (dragFloatAttribute != null)
                    {
                        if (ImGui.DragFloat(displayName, ref val, dragFloatAttribute.Step, dragFloatAttribute.Min, dragFloatAttribute.Max))
                        {
                            setter.Invoke(val);
                        }
                    }
                    break;
                case Vector2 val:
                    if (dragFloatAttribute != null)
                    {
                        if (ImGui.DragFloat2(displayName, ref val, dragFloatAttribute.Step, dragFloatAttribute.Min, dragFloatAttribute.Max))
                        {
                            setter.Invoke(val);
                        }
                    }
                    break;
                case Vector3 val:
                    if (colorAttribute != null)
                    {
                        if (ImGui.ColorEdit3(displayName, ref val, ImGuiColorEditFlags.HDR | ImGuiColorEditFlags.Float))
                        {
                            setter.Invoke(val);
                        }
                    }
                    else if (dragFloatAttribute != null)
                    {
                        if (ImGui.DragFloat3(displayName, ref val, dragFloatAttribute.Step, dragFloatAttribute.Min, dragFloatAttribute.Max))
                        {
                            setter.Invoke(val);
                        }
                    }
                    break;
                case Vector4 val:
                    if (colorAttribute != null)
                    {
                        if (ImGui.ColorEdit4(displayName, ref val, ImGuiColorEditFlags.HDR | ImGuiColorEditFlags.Float))
                        {
                            setter.Invoke(val);
                        }
                    }
                    else if (dragFloatAttribute != null)
                    {
                        if (ImGui.DragFloat4(displayName, ref val, dragFloatAttribute.Step, dragFloatAttribute.Min, dragFloatAttribute.Max))
                        {
                            setter.Invoke(val);
                        }
                    }
                    break;
                case int val:
                    if (dragIntAttribute != null)
                    {
                        if (ImGui.DragInt(displayName, ref val, dragIntAttribute.Step, dragIntAttribute.Min, dragIntAttribute.Max))
                        {
                            setter.Invoke(val);
                        }
                    }
                    break;
                case ValueTuple<int, int> val:
                    if (dragIntAttribute != null)
                    {
                        if (ImGui.DragInt2(displayName, ref val.Item1, dragIntAttribute.Step, dragIntAttribute.Min, dragIntAttribute.Max))
                        {
                            setter.Invoke(val);
                        }
                    }
                    break;
                case ValueTuple<int, int, int> val:
                    if (dragIntAttribute != null)
                    {
                        if (ImGui.DragInt3(displayName, ref val.Item1, dragIntAttribute.Step, dragIntAttribute.Min, dragIntAttribute.Max))
                        {
                            setter.Invoke(val);
                        }
                    }
                    break;
                case ValueTuple<int, int, int, int> val:
                    if (dragIntAttribute != null)
                    {
                        if (ImGui.DragInt4(displayName, ref val.Item1, dragIntAttribute.Step, dragIntAttribute.Min, dragIntAttribute.Max))
                        {
                            setter.Invoke(val);
                        }
                    }
                    break;
                case string val:
                    if (ImGui.InputText(displayName, ref val, 256))
                    {
                        setter.Invoke(val);
                    }
                    break;
                case Coocoo3DGraphics.Texture2D tex2d:
                    string rep = null;
                    object o1 = getter.Invoke();
                    if (o1 is string o2)
                    {
                        rep = o2;
                    }
                    if (ShowTexture(displayName, "global", name, ref rep, tex2d))
                    {
                        setter.Invoke(rep);
                    }
                    break;
                default:
                    ImGui.Text(displayName + " 不支持的类型");
                    break;
            }
            if (propertyOverride)
                ImGui.PopStyleColor();

            if (param.Description != null && ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text(param.Description);
                ImGui.EndTooltip();
            }
        }

        bool ShowTexture(string displayName, string id, string slot, ref string texPath, Coocoo3DGraphics.Texture2D texture = null)
        {
            bool textureChange = false;
            var cache = mainCaches;
            bool hasTexture = texPath != null && cache.TryGetTexture(texPath, out texture);

            IntPtr imageId = uiRenderSystem.ShowTexture(texture);
            ImGui.Text(displayName);
            Vector2 imageSize = new Vector2(120, 120);
            if (ImGui.ImageButton(imageId, imageSize))
            {
                StartSelectResource(id, slot);
            }
            if (CheckResourceSelect(id, slot, out string result))
            {
                cache.PreloadTexture(result);
                texPath = result;
                textureChange = true;
            }
            if (platformIO.dropFile != null && ImGui.IsItemHovered())
            {
                cache.PreloadTexture(platformIO.dropFile);
                texPath = platformIO.dropFile;
                textureChange = true;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                if (hasTexture)
                {
                    ImGui.Text(texPath);
                }
                ImGui.Image(imageId, new Vector2(256, 256));
                ImGui.EndTooltip();
            }
            return textureChange;
        }

        void RenderBuffersPannel()
        {
            if (!showRenderBuffers)
                return;
            if (ImGui.Begin("buffers", ref showRenderBuffers))
            {
                var view = windowSystem.currentChannel.renderPipelineView;
                if (view != null)
                {
                    ShowRenderBuffers(view);
                }
            }
            ImGui.End();
        }

        void ShowRenderBuffers(RenderPipelineView view)
        {
            string filter = ImFilter("filter", "filter");
            foreach (var pair in view.RenderTextures)
            {
                var tex2D = pair.Value.texture2D;
                if (tex2D != null)
                {
                    if (!Contains(pair.Key, filter))
                        continue;
                    IntPtr imageId = uiRenderSystem.ShowTexture(tex2D);

                    ImGui.TextUnformatted(pair.Key);
                    ImGui.Image(imageId, new Vector2(150, 150));

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted(tex2D.GetFormat().ToString());
                        ImGui.TextUnformatted(string.Format("width:{0} height:{1}", tex2D.width, tex2D.height));
                        ImGui.Image(imageId, new Vector2(384, 384));
                        ImGui.EndTooltip();
                    }
                }
            }
        }

        void DockSpace()
        {
            ImGuiWindowFlags window_flags = ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize
                | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoNavFocus | ImGuiWindowFlags.NoBackground;
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
            var viewPort = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewPort.WorkPos, ImGuiCond.Always);
            ImGui.SetNextWindowSize(viewPort.Size, ImGuiCond.Always);
            ImGui.SetNextWindowViewport(viewPort.ID);

            if (ImGui.Begin("Dockspace", window_flags))
            {
                var tex = windowSystem.visualChannels.FirstOrDefault().Value.GetAOV(Caprice.Attributes.AOVType.Color);
                IntPtr imageId = uiRenderSystem.ShowTexture(tex);
                ImGuiDockNodeFlags dockNodeFlag = ImGuiDockNodeFlags.PassthruCentralNode;
                ImGui.GetWindowDrawList().AddImage(imageId, viewPort.WorkPos, viewPort.WorkPos + viewPort.WorkSize);
                ImGui.DockSpace(ImGui.GetID("MyDockSpace"), Vector2.Zero, dockNodeFlag);
            }
            ImGui.End();
            ImGui.PopStyleVar(3);
        }

        static FileInfo Resources()
        {
            if (ImGui.Button("打开文件夹"))
            {
                requireOpenFolder = true;
            }
            ImGui.SameLine();
            if (ImGui.Button("刷新"))
            {
                viewRequest = currentFolder;
            }
            ImGui.SameLine();
            if (ImGui.Button("后退"))
            {
                if (navigationStack.Count > 0)
                    viewRequest = navigationStack.Pop();
            }
            string filter = ImFilter("查找文件", "查找文件");

            ImGuiTableFlags tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersV |
                ImGuiTableFlags.Resizable | ImGuiTableFlags.Reorderable | ImGuiTableFlags.Hideable | ImGuiTableFlags.ScrollY;

            var windowSize = ImGui.GetWindowSize();
            var itemSize = windowSize - ImGui.GetCursorPos();
            itemSize.X = 0;
            itemSize.Y -= 8;

            FileInfo open1 = null;
            if (ImGui.BeginTable("resources", 2, tableFlags, Vector2.Max(itemSize, new Vector2(0, 28)), 0))
            {
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableSetupColumn("文件名");
                ImGui.TableSetupColumn("大小");
                ImGui.TableHeadersRow();

                lock (storageItems)
                {
                    bool _requireClear = false;
                    foreach (var item in storageItems)
                    {
                        if (!Contains(item.Name, filter))
                            continue;
                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0);
                        DirectoryInfo folder = item as DirectoryInfo;
                        FileInfo file = item as FileInfo;
                        if (ImGui.Selectable(item.Name, false, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowDoubleClick) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                        {
                            if (folder != null)
                            {
                                navigationStack.Push(currentFolder);
                                viewRequest = folder;
                                _requireClear = true;
                            }
                            else if (file != null)
                            {
                                open1 = file;
                            }
                            ImGui.SaveIniSettingsToDisk("imgui.ini");
                        }
                        ImGui.TableSetColumnIndex(1);
                        if (file != null)
                        {
                            ImGui.TextUnformatted(string.Format("{0} KB", (file.Length + 1023) / 1024));
                        }
                    }
                    if (_requireClear)
                        storageItems.Clear();
                }
                ImGui.EndTable();
            }

            return open1;
        }

        static void Help()
        {
            if (ImGui.TreeNode("基本操作"))
            {
                ImGui.Text(@"旋转视角 - 按住鼠标右键拖动
平移镜头 - 按住鼠标中键拖动
拉近、拉远镜头 - 鼠标滚轮
修改物体位置、旋转 - 双击修改，或者在数字上按住左键然后拖动
打开文件 - 将文件拖入窗口，或者在资源窗口打开文件夹。");
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("支持格式"))
            {
                ImGui.Text(@"当前版本支持pmx、glTF格式模型，
vmd格式动作。支持几乎所有的图片格式。");
                ImGui.TreePop();
            }
            ImGui.Checkbox("显示ImGuiDemoWindow", ref demoWindowOpen);
            ImGui.Checkbox("显示Render Buffers", ref showRenderBuffers);
        }

        void SceneHierarchy()
        {
            if (ImGui.Button("新光源"))
            {
                CurrentScene.NewLighting();
            }
            ImGui.SameLine();
            if (ImGui.Button("新粒子"))
            {
                NewParticle();
            }
            ImGui.SameLine();
            if (ImGui.Button("新贴花"))
            {
                CurrentScene.NewDecal();
            }
            //ImGui.SameLine();
            bool removeObject = false;
            if (ImGui.Button("移除物体") || (ImGui.IsKeyPressed((int)ImGuiKey.Delete) && ImGui.IsWindowHovered()))
            {
                removeObject = true;
            }
            bool copyObject = false; ;
            ImGui.SameLine();
            if (ImGui.Button("复制物体"))
            {
                copyObject = true;
            }
            //while (gameObjectSelected.Count < main.CurrentScene.gameObjects.Count)
            //{
            //    gameObjectSelected.Add(false);
            //}
            string filter = ImFilter("查找物体", "查找名称");
            var gameObjects = CurrentScene.world;
            int i = 0;
            foreach (var gameObject in gameObjects)
            {
                TryGetComponent(gameObject, out ObjectDescription objectDescription);
                string name = objectDescription == null ? "object" : objectDescription.Name;

                if (!Contains(name, filter))
                {
                    i++;
                    continue;
                }
                bool selected = gameObjectSelectIndex == i;
                bool selected1 = ImGui.Selectable(name + "###" + gameObject.GetHashCode(), ref selected);
                //if (ImGui.IsItemActive() && !ImGui.IsItemHovered())
                //{
                //    int n_next = i + (ImGui.GetMouseDragDelta(0).Y < 0.0f ? -1 : 1);
                //    if (n_next >= 0 && n_next < gameObjects.Count)
                //    {
                //        gameObjects[i] = gameObjects[n_next];
                //        gameObjects[n_next] = gameObject;
                //        ImGui.ResetMouseDragDelta();
                //    }
                //}
                if (selected1 || CurrentScene.SelectedGameObjects.Count < 1)
                {
                    gameObjectSelectIndex = i;
                    CurrentScene.SelectedGameObjects.Clear();
                    CurrentScene.SelectedGameObjects.Add(gameObject.GetHashCode());
                }
                i++;
            }
            if (removeObject)
            {
                foreach (var entity in CurrentScene.world)
                {
                    if (CurrentScene.SelectedGameObjects.Contains(entity.GetHashCode()))
                        recorder.Record(entity).Dispose();
                }
                CurrentScene.SelectedGameObjects.Clear();

                //if (gameObjects.Count > gameObjectSelectIndex + 1)
                //    CurrentScene.SelectedGameObjects.Add(gameObjects[gameObjectSelectIndex + 1]);
            }
            if (copyObject)
            {
                foreach (var gameObject in CurrentScene.world)
                    if (CurrentScene.SelectedGameObjects.Contains(gameObject.GetHashCode()))
                        CurrentScene.DuplicateObject(gameObject);
            }
        }

        void GameObjectPanel(Entity gameObject)
        {
            TryGetComponent<MMDRendererComponent>(gameObject, out var renderer);
            TryGetComponent<MeshRendererComponent>(gameObject, out var meshRenderer);
            TryGetComponent<VisualComponent>(gameObject, out var visual);
            TryGetComponent<AnimationStateComponent>(gameObject, out var animationState);
            TryGetComponent<ObjectDescription>(gameObject, out var objectDescription);

            ImGui.InputText("名称", ref objectDescription.Name, 256);
            if (ImGui.TreeNode("描述"))
            {
                ImGui.Text(objectDescription.Description);
                if (renderer != null)
                {
                    var mesh = mainCaches.GetModel(renderer.meshPath).GetMesh();
                    ImGui.Text(string.Format("顶点数：{0} 索引数：{1} 材质数：{2}\n模型文件：{3}\n",
                        mesh.GetVertexCount(), mesh.GetIndexCount(), renderer.Materials.Count,
                        renderer.meshPath));
                }

                ImGui.TreePop();
            }
            if (ImGui.TreeNode("变换"))
            {
                if (ImGui.DragFloat3("位置", ref position, 0.01f))
                {
                    positionChange = true;
                }
                Vector3 a = rotation / MathF.PI * 180;
                if (ImGui.DragFloat3("旋转", ref a))
                {
                    rotation = a * MathF.PI / 180;
                    rotationChange = true;
                }
                ImGui.TreePop();
            }
            if (renderer != null)
            {
                RendererComponent(renderer, animationState);
            }
            if (meshRenderer != null)
            {
                RendererComponent(meshRenderer);
            }
            if (visual != null)
            {
                VisualComponent(ref gameObject.Get<Transform>(), visual);
            }
        }

        void RendererComponent(MMDRendererComponent renderer, AnimationStateComponent animationState)
        {
            if (ImGui.TreeNode("材质"))
            {
                ShowMaterials(mainCaches.GetModel(renderer.meshPath).Submeshes, renderer.Materials);
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("动画"))
            {
                ImGui.Text(string.Format("动作文件：{0}", animationState.motionPath));
                if (ImGui.Button("清除动画"))
                {
                    gameDriverContext.RequireResetPhysics = true;
                    animationState.motionPath = "";
                }
                ImGui.Checkbox("蒙皮", ref renderer.skinning);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("关闭蒙皮可以提高性能");
                if (ImGui.Checkbox("使用IK", ref renderer.enableIK))
                {
                    gameDriverContext.RequireResetPhysics = true;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("如果动作不使用IK，请取消勾选");
                ImGui.Checkbox("锁定动作", ref animationState.LockMotion);
                if (animationState.LockMotion)
                {
                    string filter = ImFilter("搜索变形", "搜索变形");
                    for (int i = 0; i < renderer.Morphs.Count; i++)
                    {
                        MorphDesc morpth = renderer.Morphs[i];
                        if (!Contains(morpth.Name, filter)) continue;
                        if (ImGui.SliderFloat(morpth.Name, ref animationState.Weights.Origin[i], 0, 1))
                        {
                            gameDriverContext.RequireResetPhysics = true;
                        }
                    }
                }
                ImGui.TreePop();
            }
        }

        void RendererComponent(MeshRendererComponent renderer)
        {
            if (ImGui.TreeNode("材质"))
            {
                ShowMaterials(mainCaches.GetModel(renderer.meshPath).Submeshes, renderer.Materials);
                ImGui.TreePop();
            }
        }

        void ShowMaterials(List<Submesh> submeshes, List<RenderMaterial> materials)
        {
            if (ImGui.BeginChild("materials", new Vector2(120, 400)))
            {
                ImGui.PushItemWidth(120);
                for (int i = 0; i < materials.Count; i++)
                {
                    var submesh = submeshes[i];
                    bool selected = i == materialSelectIndex;
                    ImGui.Selectable(string.Format("{0}##{1}", submesh.Name, i), ref selected);
                    if (selected) materialSelectIndex = i;
                }
                ImGui.PopItemWidth();
            }
            ImGui.EndChild();
            ImGui.SameLine();
            if (ImGui.BeginChild("materialProperty", new Vector2(200, 400)))
            {
                if (materialSelectIndex >= 0 && materialSelectIndex < materials.Count)
                {
                    var material = materials[materialSelectIndex];
                    var submesh = submeshes[materialSelectIndex];
                    ImGui.Text(submesh.Name);
                    if (ImGui.Button("修改此物体所有材质"))
                    {
                        StartEditParam();
                    }

                    ShowParams(UIShowType.Material, windowSystem.currentChannel.renderPipelineView, material.Parameters);
                }
            }
            ImGui.EndChild();
        }

        void VisualComponent(ref Transform transform, VisualComponent visualComponent)
        {
            if (ImGui.TreeNode("绑定"))
            {
                int rendererCount = renderPipelineContext.renderers.Count;
                string[] renderers = new string[rendererCount + 1];
                int[] ids = new int[rendererCount + 1];
                renderers[0] = "-";
                ids[0] = -1;
                int count = 1;
                int currentItem = 0;

                foreach (var gameObject1 in CurrentScene.world)
                {
                    if (!TryGetComponent<MMDRendererComponent>(gameObject1, out var renderer))
                        continue;
                    TryGetComponent<ObjectDescription>(gameObject1, out var desc);
                    renderers[count] = desc == null ? "object" : desc.Name;
                    ids[count] = gameObject1.GetHashCode();
                    if (gameObject1.GetHashCode() == visualComponent.bindId)
                        currentItem = count;
                    count++;
                    if (count == rendererCount + 1)
                        break;
                }
                if (ImGui.Combo("绑定到物体", ref currentItem, renderers, rendererCount + 1))
                {
                    visualComponent.bindId = ids[currentItem];
                }


                string[] bones;
                if (renderPipelineContext.gameObjects.TryGetValue(visualComponent.bindId, out var gameObject2))
                {
                    var renderer = gameObject2.Get<MMDRendererComponent>();
                    bones = new string[renderer.bones.Count + 1];
                    bones[0] = "-";
                    for (int i = 0; i < renderer.bones.Count; i++)
                    {
                        bones[i + 1] = renderer.bones[i].Name;
                    }
                }
                else
                {
                    bones = new string[0];
                }
                currentItem = 0;
                for (int i = 1; i < bones.Length; i++)
                {
                    if (bones[i] == visualComponent.bindBone)
                        currentItem = i;
                }
                if (ImGui.Combo("绑定骨骼", ref currentItem, bones, bones.Length))
                {
                    if (currentItem > 0)
                    {
                        visualComponent.bindBone = bones[currentItem];
                    }
                    else
                    {
                        visualComponent.bindBone = null;
                    }
                }
                ImGui.Checkbox("绑定X", ref visualComponent.bindX);
                ImGui.Checkbox("绑定Y", ref visualComponent.bindY);
                ImGui.Checkbox("绑定Z", ref visualComponent.bindZ);
                ImGui.Checkbox("绑定旋转", ref visualComponent.bindRot);
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("视觉"))
            {
                ImGui.Checkbox("显示包围盒", ref showBounding);

                ImGui.DragFloat3("大小", ref transform.scale, 0.01f);
                ShowParams(visualComponent.UIShowType, windowSystem.currentChannel.renderPipelineView, visualComponent.material.Parameters);
                if (visualComponent.UIShowType == UIShowType.Particle)
                {

                }
                ImGui.TreePop();
            }
        }

        void SceneView(VisualChannel channel, float mouseWheelDelta, Vector2 mouseMoveDelta)
        {
            var io = ImGui.GetIO();
            var tex = channel.GetAOV(Caprice.Attributes.AOVType.Color);
            Vector2 texSize;
            IntPtr imageId;
            if (tex != null)
            {
                texSize = new Vector2(tex.width, tex.height);
                imageId = uiRenderSystem.ShowTexture(tex);
            }
            else
            {
                texSize = new Vector2(0, 0);
                imageId = uiRenderSystem.ShowTexture(null);
            }

            Vector2 pos = ImGui.GetCursorScreenPos();
            Vector2 spaceSize = Vector2.Max(ImGui.GetWindowSize() - new Vector2(20, 40), new Vector2(100, 100));
            channel.sceneViewSize = ((int)spaceSize.X, (int)spaceSize.Y);
            float factor = MathF.Max(MathF.Min(spaceSize.X / texSize.X, spaceSize.Y / texSize.Y), 0.01f);
            Vector2 imageSize = texSize * factor;


            ImGui.InvisibleButton("X", imageSize, ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight | ImGuiButtonFlags.MouseButtonMiddle);
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddImage(imageId, pos, pos + imageSize);
            DrawGizmo(channel, pos, imageSize);

            var camera = channel.camera;
            if (ImGui.IsItemActive())
            {
                if (io.MouseDown[1])
                {
                    if (io.KeyCtrl)
                        camera.Distance += (-mouseMoveDelta.Y / 150);
                    else
                        camera.RotateDelta(new Vector3(-mouseMoveDelta.Y, mouseMoveDelta.X, 0) / 200);
                }
                if (io.MouseDown[2])
                {
                    if (io.KeyCtrl)
                        camera.MoveDelta(new Vector3(mouseMoveDelta.X, mouseMoveDelta.Y, 0) / 40);
                    else if (io.KeyShift)
                        camera.MoveDelta(new Vector3(mouseMoveDelta.X, mouseMoveDelta.Y, 0) / 4000);
                    else
                        camera.MoveDelta(new Vector3(mouseMoveDelta.X, mouseMoveDelta.Y, 0) / 400);
                }
                windowSystem.currentChannel = channel;
            }
            if (ImGui.IsItemHovered())
            {
                camera.Distance += mouseWheelDelta * 0.6f;
                if (platformIO.dropFile != null)
                {
                    openRequest = new FileInfo(platformIO.dropFile);
                }
            }
        }

        void DrawGizmo(VisualChannel channel, Vector2 imagePosition, Vector2 imageSize)
        {
            var io = ImGui.GetIO();
            Vector2 mousePos = ImGui.GetMousePos();
            int hoveredIndex = -1;
            string toolTipMessage = "";
            var scene = CurrentScene;
            var vpMatrix = channel.cameraData.vpMatrix;

            UIViewport viewport = new UIViewport
            {
                leftTop = imagePosition,
                rightBottom = imagePosition + imageSize,
            };

            ImGui.PushClipRect(viewport.leftTop, viewport.rightBottom, true);
            var drawList = ImGui.GetWindowDrawList();
            bool hasDrag = false;

            int i = 0;
            foreach (var obj in scene.world)
            {
                ref var transform = ref obj.Get<Transform>();
                var objectDescription = obj.Get<ObjectDescription>();
                Vector3 position = transform.position;
                Vector2 basePos = imagePosition + (ImGuiExt.TransformToImage(position, vpMatrix, out bool canView)) * imageSize;
                Vector2 diff = Vector2.Abs(basePos - mousePos);
                if (diff.X < 10 && diff.Y < 10 && canView)
                {
                    toolTipMessage += objectDescription.Name + "\n";
                    hoveredIndex = i;
                    drawList.AddNgon(basePos, 10, 0xffffffff, 4);
                }
                if (gameObjectSelectIndex == i && canView)
                {
                    viewport.mvp = vpMatrix;
                    bool drag = ImGuiExt.PositionController(drawList, ref UIImGui.position, io.MouseDown[0], viewport);
                    if (drag)
                    {
                        positionChange = true;
                        hasDrag = true;
                    }
                }
                if (TryGetComponent(obj, out VisualComponent visual) && showBounding)
                {
                    viewport.mvp = transform.GetMatrix() * vpMatrix;
                    ImGuiExt.DrawCube(drawList, viewport);
                }
                i++;
            }
            ImGui.PopClipRect();

            //if (ImGui.IsItemHovered())
            //{
            //    if (io.MouseReleased[0] && ImGui.IsItemFocused() && !hasDrag)
            //    {
            //        gameObjectSelectIndex = hoveredIndex;
            //        if (hoveredIndex != -1)
            //        {
            //            CurrentScene.SelectedGameObjects.Clear();
            //            CurrentScene.SelectedGameObjects.Add(scene.gameObjects[hoveredIndex]);
            //        }
            //    }
            //}
            if (!string.IsNullOrEmpty(toolTipMessage))
            {
                ImGui.BeginTooltip();
                ImGui.Text(toolTipMessage);
                ImGui.EndTooltip();
            }
        }

        static void StartSelectResource(string id, string slot)
        {
            requestOpenResource = true;
            fileOpenId = id;
            fileOpenSlot = slot;
        }

        static bool CheckResourceSelect(string id, string slot, out string selectedResource)
        {
            if (id == fileOpenId && slot == fileOpenSlot && fileOpenResult != null)
            {
                selectedResource = fileOpenResult;
                fileOpenResult = null;
                fileOpenId = null;
                fileOpenSlot = null;
                return true;
            }
            else
                selectedResource = null;
            return false;
        }

        static void StartEditParam()
        {
            paramEdit = new Dictionary<string, object>();
            requestParamEdit = true;
        }

        void Popups(Entity gameObject)
        {
            if (requestOpenResource)
            {
                requestOpenResource = false;
                ImGui.OpenPopup("选择资源");
                popupOpenResource = true;
            }
            ImGui.SetNextWindowSize(new Vector2(400, 400), ImGuiCond.Appearing);
            if (ImGui.BeginPopupModal("选择资源", ref popupOpenResource))
            {
                if (ImGui.Button("关闭"))
                {
                    popupOpenResource = false;
                }
                var _open = Resources();
                if (_open != null)
                {
                    fileOpenResult = _open.FullName;
                    popupOpenResource = false;
                }
                ImGui.EndPopup();
            }
            if (requestParamEdit)
            {
                requestParamEdit = false;
                ImGui.OpenPopup("编辑参数");
                popupParamEdit = true;
            }
            if (ImGui.BeginPopupModal("编辑参数", ref popupParamEdit))
            {
                ShowParams(UIShowType.Material, windowSystem.currentChannel.renderPipelineView, paramEdit);

                if (ImGui.Button("确定"))
                {
                    TryGetComponent<MeshRendererComponent>(gameObject, out var meshRenderer);
                    TryGetComponent<MMDRendererComponent>(gameObject, out var mmdRenderer);
                    IList<RenderMaterial> materials = null;
                    if (meshRenderer != null)
                    {
                        materials = meshRenderer.Materials;
                    }
                    if (mmdRenderer != null)
                    {
                        materials = mmdRenderer.Materials;
                    }
                    foreach (var material in materials)
                        foreach (var param in paramEdit)
                        {
                            material.Parameters[param.Key] = param.Value;
                        }

                    paramEdit = null;

                    popupParamEdit = false;
                }
                if (ImGui.Button("取消"))
                {
                    popupParamEdit = false;
                }
                ImGui.EndPopup();
            }
        }

        void NewParticle()
        {
            var world = CurrentScene.recorder.Record(CurrentScene.world);
            var gameObject = world.CreateEntity();
            VisualComponent component = new VisualComponent();
            component.UIShowType = UIShowType.Particle;
            component.material.Parameters["ParticleCount"] = 100;
            component.material.Parameters["ParticleLife"] = new Vector2(5.0f, 15.0f);
            component.material.Parameters["ParticleRandomSpeed"] = new Vector2(0.003f, 0.03f);
            component.material.Parameters["ParticleInitialSpeed"] = new Vector3(0, 0, 0);
            component.material.Parameters["ParticleScale"] = new Vector2(0.02f, 0.02f);
            component.material.Parameters["ParticleAcceleration"] = new Vector3(0, 0, 0);
            gameObject.Set(component);
            gameObject.Set(new ObjectDescription
            {
                Name = "粒子",
                Description = ""
            });
            gameObject.Set(new Transform(new Vector3(0, 1, 0), Quaternion.CreateFromYawPitchRoll(0, 0, 0), new Vector3(1, 1, 1)));
        }

        static bool TryGetComponent<T>(Entity obj, out T value)
        {
            if (obj.Has<T>())
            {
                value = obj.Get<T>();
                return true;
            }
            else
            {
                value = default(T);
                return false;
            }
        }

        static string ImFilter(string lable, string hint)
        {
            uint id = ImGui.GetID(lable);
            string filter = filters.GetValueOrDefault(id, "");
            if (ImGui.InputTextWithHint(lable, hint, ref filter, 128))
            {
                filters[id] = filter;
            }
            return filter;
        }

        static string fileOpenId = null;

        public static bool demoWindowOpen = false;
        public static Vector3 position;
        public static Vector3 rotation;
        public static Vector3 scale;
        public static Quaternion rotationCache;
        public static bool rotationChange;
        public static bool positionChange;
        public static bool scaleChange;

        public static int materialSelectIndex = 0;
        public static int gameObjectSelectIndex = -1;
        public static bool requireOpenFolder;
        public static bool requestRecord;
        public static bool requestSave;

        public static bool requestSelectRenderPipelines;

        public static Stack<DirectoryInfo> navigationStack = new Stack<DirectoryInfo>();
        public static List<FileSystemInfo> storageItems = new List<FileSystemInfo>();
        public static DirectoryInfo currentFolder;
        public static DirectoryInfo viewRequest;
        public static DirectoryInfo loadRPRequest;
        public static FileInfo openRequest;
        //public static List<bool> gameObjectSelected = new List<bool>();

        static Vector3 QuaternionToEularYXZ(Quaternion quaternion)
        {
            double ii = (double)quaternion.X * quaternion.X;
            double jj = (double)quaternion.Y * quaternion.Y;
            double kk = (double)quaternion.Z * quaternion.Z;
            double ei = (double)quaternion.W * quaternion.X;
            double ej = (double)quaternion.W * quaternion.Y;
            double ek = (double)quaternion.W * quaternion.Z;
            double ij = (double)quaternion.X * quaternion.Y;
            double ik = (double)quaternion.X * quaternion.Z;
            double jk = (double)quaternion.Y * quaternion.Z;
            Vector3 result = new Vector3();
            result.X = (float)Math.Asin(2.0 * (ei - jk));
            result.Y = (float)Math.Atan2(2.0 * (ej + ik), 1 - 2.0 * (ii + jj));
            result.Z = (float)Math.Atan2(2.0 * (ek + ij), 1 - 2.0 * (ii + kk));
            return result;
        }

        public void Initialize()
        {
            var caches = mainCaches;
            ImGui.SetCurrentContext(ImGui.CreateContext());
            Coocoo3DGraphics.Uploader uploader = new Coocoo3DGraphics.Uploader();
            var io = ImGui.GetIO();
            io.Fonts.AddFontFromFileTTF("c:\\Windows\\Fonts\\SIMHEI.ttf", 14, null, io.Fonts.GetGlyphRangesChineseFull());
            unsafe
            {
                byte* data;
                io.Fonts.GetTexDataAsRGBA32(out data, out int width, out int height, out int bytesPerPixel);
                int size = width * height * bytesPerPixel;
                Span<byte> spanByte1 = new Span<byte>(data, size);

                uploader.Texture2DRaw(spanByte1, Vortice.DXGI.Format.R8G8B8A8_UNorm, width, height);
            }

            var texture2D = uiRenderSystem.uiTexture = new Coocoo3DGraphics.Texture2D();
            io.Fonts.TexID = new IntPtr(UIRenderSystem.uiTextureIndex);
            caches.uploadHandler.Add(new GpuUploadTask(texture2D, uploader));
            InitKeyMap();
        }

        static void InitKeyMap()
        {
            var io = ImGui.GetIO();

            io.KeyMap[(int)ImGuiKey.Tab] = (int)ImGuiKey.Tab;
            io.KeyMap[(int)ImGuiKey.LeftArrow] = (int)ImGuiKey.LeftArrow;
            io.KeyMap[(int)ImGuiKey.RightArrow] = (int)ImGuiKey.RightArrow;
            io.KeyMap[(int)ImGuiKey.UpArrow] = (int)ImGuiKey.UpArrow;
            io.KeyMap[(int)ImGuiKey.DownArrow] = (int)ImGuiKey.DownArrow;
            io.KeyMap[(int)ImGuiKey.PageUp] = (int)ImGuiKey.PageUp;
            io.KeyMap[(int)ImGuiKey.PageDown] = (int)ImGuiKey.PageDown;
            io.KeyMap[(int)ImGuiKey.Home] = (int)ImGuiKey.Home;
            io.KeyMap[(int)ImGuiKey.End] = (int)ImGuiKey.End;
            io.KeyMap[(int)ImGuiKey.Insert] = (int)ImGuiKey.Insert;
            io.KeyMap[(int)ImGuiKey.Delete] = (int)ImGuiKey.Delete;
            io.KeyMap[(int)ImGuiKey.Backspace] = (int)ImGuiKey.Backspace;
            io.KeyMap[(int)ImGuiKey.Space] = (int)ImGuiKey.Space;
            io.KeyMap[(int)ImGuiKey.Enter] = (int)ImGuiKey.Enter;
            io.KeyMap[(int)ImGuiKey.Escape] = (int)ImGuiKey.Escape;
            io.KeyMap[(int)ImGuiKey.KeyPadEnter] = (int)ImGuiKey.KeyPadEnter;
            io.KeyMap[(int)ImGuiKey.A] = 'A';
            io.KeyMap[(int)ImGuiKey.C] = 'C';
            io.KeyMap[(int)ImGuiKey.V] = 'V';
            io.KeyMap[(int)ImGuiKey.X] = 'X';
            io.KeyMap[(int)ImGuiKey.Y] = 'Y';
            io.KeyMap[(int)ImGuiKey.Z] = 'Z';

            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        }

        static bool requestOpenResource = false;
        static bool popupOpenResource = false;
        static string fileOpenResult;
        static string fileOpenSlot;

        static bool requestParamEdit = false;
        static bool popupParamEdit = false;
        static bool showBounding = false;
        static bool showRenderBuffers;
        static Dictionary<string, object> paramEdit;

        static Dictionary<uint, string> filters = new Dictionary<uint, string>();

        static bool Contains(string input, string filter)
        {
            return input.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
        }


        public void Play()
        {
            gameDriverContext.Playing = true;
            gameDriverContext.PlaySpeed = 1.0f;
            gameDriverContext.RequireRender(false);
        }
        public void Pause()
        {
            gameDriverContext.Playing = false;
        }
        public void Stop()
        {
            gameDriver.ToPlayMode();
            gameDriverContext.Playing = false;
            gameDriverContext.PlayTime = 0;
            gameDriverContext.RequireRender(true);
        }
        public void Rewind()
        {
            gameDriver.ToPlayMode();
            gameDriverContext.Playing = true;
            gameDriverContext.PlaySpeed = -2.0f;
        }
        public void FastForward()
        {
            gameDriver.ToPlayMode();
            gameDriverContext.Playing = true;
            gameDriverContext.PlaySpeed = 2.0f;
        }
        public void Front()
        {
            gameDriver.ToPlayMode();
            gameDriverContext.PlayTime = 0;
            gameDriverContext.RequireRender(true);
        }
        public void Rear()
        {
            gameDriver.ToPlayMode();
            gameDriverContext.PlayTime = 9999;
            gameDriverContext.RequireRender(true);
        }
    }
}
