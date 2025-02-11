﻿using Coocoo3D.Common;
using Coocoo3D.RenderPipeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Core
{
    public class GameDriverContext
    {
        public int NeedRender;
        public bool Playing;
        public double PlayTime;
        public double DeltaTime;
        public double RealDeltaTime;
        public float FrameInterval;
        public float PlaySpeed;
        public bool RequireResetPhysics;
        public TimeManager timeManager;

        public void RequireRender(bool updateEntities)
        {
            if (updateEntities)
                RequireResetPhysics = true;
            NeedRender = 10;
        }
    }

    public class GameDriver
    {
        public GameDriverContext gameDriverContext;

        public WindowSystem windowSystem;

        public RecordSystem recordSystem;

        public RenderPipelineContext renderPipelineContext;

        public bool Next()
        {
            var recordSettings = recordSystem.recordSettings;

            if (toRecordMode)
            {
                toRecordMode = false;
                renderPipelineContext.recording = true;
                gameDriverContext.Playing = true;
                gameDriverContext.PlaySpeed = 1.0f;
                gameDriverContext.PlayTime = 0.0f;
                gameDriverContext.RequireResetPhysics = true;

                StartTime = recordSettings.StartTime;
                StopTime = recordSettings.StopTime;
                RenderCount = 0;
                FrameIntervalF = 1 / MathF.Max(recordSettings.FPS, 1e-3f);

                recordSystem.saveDirectory = saveDirectory;
                recordSystem.StartRecord();
            }
            if (toPlayMode)
            {
                toPlayMode = false;
                renderPipelineContext.recording = false;
                recordSystem.StopRecord();
            }
            bool returnValue;
            if (renderPipelineContext.recording)
                returnValue = Recording();
            else
                returnValue = Playing();

            renderPipelineContext.RealDeltaTime = gameDriverContext.RealDeltaTime;
            renderPipelineContext.Time = gameDriverContext.PlayTime;
            renderPipelineContext.DeltaTime = gameDriverContext.Playing ? gameDriverContext.DeltaTime : 0;
            Update();
            return returnValue;
        }

        bool Playing()
        {
            GameDriverContext context = gameDriverContext;
            var timeManager = context.timeManager;
            if (!timeManager.RealTimerCorrect("frame", context.FrameInterval, out double deltaTime))
            {
                return false;
            }
            if (!(context.NeedRender > 0 || context.Playing))
            {
                return false;
            }
            context.NeedRender -= 1;

            context.RealDeltaTime = deltaTime;
            context.DeltaTime = Math.Clamp(deltaTime * context.PlaySpeed, -0.17f, 0.17f);
            if (context.Playing)
                context.PlayTime += context.DeltaTime;
            return true;
        }

        bool Recording()
        {
            gameDriverContext.NeedRender = 1;

            gameDriverContext.RealDeltaTime = FrameIntervalF;
            gameDriverContext.DeltaTime = FrameIntervalF;
            gameDriverContext.PlayTime = FrameIntervalF * RenderCount;

            return true;
        }

        void Update()
        {
            if (!renderPipelineContext.recording) return;
            if (!windowSystem.visualChannels.TryGetValue(recordChannel, out var visualChannel1))
            {
                ToPlayMode();
                return;
            }

            if (gameDriverContext.PlayTime > StopTime)
                ToPlayMode();

            RenderCount++;
        }

        public void RequireRender(bool updateEntities)
        {
            if (updateEntities)
                gameDriverContext.RequireResetPhysics = true;
            gameDriverContext.NeedRender = 10;
        }

        public float StartTime;
        public float StopTime;

        public float FrameIntervalF = 1 / 60.0f;
        int RenderCount = 0;
        bool toRecordMode;
        bool toPlayMode;
        public string saveDirectory;
        public string recordChannel = "main";

        public bool IsEnabled { get; set; } = true;

        public void ToRecordMode(string saveDirectory)
        {
            toRecordMode = true;
            this.saveDirectory = saveDirectory;
        }
        public void ToPlayMode()
        {
            toPlayMode = true;
        }

    }
}
