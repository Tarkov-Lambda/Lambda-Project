//
// Copyright 2017-2023 Valve Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System.Threading.Tasks;

using UnityEngine;

namespace SteamAudio
{
    public class SteamAudioStaticMesh : MonoBehaviour
    {
        [Header("Export Settings")]
        public SerializedData asset = null;
        public string sceneNameWhenExported = "";

#if STEAMAUDIO_ENABLED
        StaticMesh mStaticMesh = null;
        Task<StaticMesh> mTask = null;
        bool mShouldLoadAsync = false;

        void Start()
        {
            // Guard: if the manager didn't initialize (e.g. missing SteamAudioSettings asset
            // in Tarkov, or non-standard audio engine), skip static mesh loading entirely.
            if (SteamAudioManager.Singleton == null || SteamAudioManager.Context == null)
            {
                Debug.LogWarning("[SteamAudio] SteamAudioStaticMesh.Start: SteamAudioManager not ready, static mesh will not be loaded.");
                return;
            }

            if (asset == null)
            {
                Debug.LogWarningFormat("No asset set for Steam Audio Static Mesh in scene {0}. Export the scene before clicking Play.",
                    gameObject.scene.name);
            }

            // Only load the static mesh asynchronously if we're using the default scene type. In particular, with
            // Embree, explicit synchronization would be required, such that if a Task is loading a static mesh
            // asynchronously, then we don't run any simulations on the simulation thread at the same time.
            if (SteamAudioManager.GetSceneType() == SceneType.Default)
            {
                mShouldLoadAsync = true;
            }
        }

        void OnDestroy()
        {
            if (mStaticMesh != null)
            {
                mStaticMesh.Release();
            }
            else if (mTask != null)
            {
                mTask.ContinueWith(static e => e.Result.Release());
            }
        }

        void OnEnable()
        {
            if (mStaticMesh != null && SteamAudioManager.CurrentScene != null)
            {
                mStaticMesh.AddToScene(SteamAudioManager.CurrentScene);
                SteamAudioManager.ScheduleCommitScene();
            }
        }

        void OnDisable()
        {
            if (mStaticMesh != null && SteamAudioManager.CurrentScene != null)
            {
                mStaticMesh.RemoveFromScene(SteamAudioManager.CurrentScene);
                SteamAudioManager.ScheduleCommitScene();
            }
        }

        void Update()
        {
            // Guard: don't try to load if the context or scene isn't available yet.
            if (SteamAudioManager.Context == null || SteamAudioManager.CurrentScene == null) return;

            if (mStaticMesh == null && asset != null)
            {
                if (mShouldLoadAsync)
                {
                    if (mTask == null)
                    {
                        // Capture context and scene at task-creation time so the background thread
                        // doesn't race against a potential null inside the static accessor.
                        var ctx = SteamAudioManager.Context;
                        var scene = SteamAudioManager.CurrentScene;
                        var a = asset;
                        mTask = Task.Run(() => new StaticMesh(ctx, scene, a));
                    }
                    else if (mTask.IsCompleted)
                    {
                        mStaticMesh = mTask.Result;
                        mTask = null;
                        if (enabled && SteamAudioManager.CurrentScene != null)
                        {
                            mStaticMesh.AddToScene(SteamAudioManager.CurrentScene);
                            SteamAudioManager.ScheduleCommitScene();
                        }
                    }
                }
                else
                {
                    mStaticMesh = new StaticMesh(SteamAudioManager.Context, SteamAudioManager.CurrentScene, asset);
                    if (enabled)
                    {
                        mStaticMesh.AddToScene(SteamAudioManager.CurrentScene);
                        SteamAudioManager.ScheduleCommitScene();
                    }
                }
            }
        }
#endif
    }
}
