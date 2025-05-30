using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Modding;
using JetBrains.Annotations;
using UnityEngine.SceneManagement;
using Random = System.Random;
using USceneManager = UnityEngine.SceneManagement.SceneManager;
using Vasi;

namespace RandomPantheons
{
    [UsedImplicitly]
    public class RandomPantheons : Mod, ITogglableMod
    {
        // Some scenes cause issues when first.
        private static readonly List<string> InvalidFirst =
        [
            "GG_Spa",
            // Doesn't wake up
            "GG_Gruz_Mother",
            "GG_Gruz_Mother_V"
        ];

        // Some scenes cause issues when last
        private static readonly List<string> InvalidLast =
        [
            "GG_Unn",
            "GG_Wyrm",
            "GG_Engine",
            "GG_Engine_Prime",
            "GG_Engine_Root",
            // Causes an infinite loop
            "GG_Spa"
        ];
        
        private static readonly List<string> VanishedHUD =
        [
            "GG_Hollow_Knight",
            "GG_Radiance"
        ];

        private readonly Random _rand = new();

        public override string GetVersion() => VersionUtil.GetVersion<RandomPantheons>();

        public override void Initialize()
        {
            /*
             * We need to hook on the door, rather than the controller
             * Because the door has the challengeFSM responsible for the transition to the first scene
             * Which is otherwise ignored if we edit the sequence later.
             */
            On.BossSequenceDoor.Start += RandomizeSequence;
            // Fixes the door for P5
            On.PlayMakerFSM.Start += ModifyRadiance;

            USceneManager.activeSceneChanged += OnSceneChange;
        }

        private void OnSceneChange(Scene prev, Scene next)
        {
            if (GameManager.instance == null)
                return;

            IEnumerator CheckHud()
            {
                yield return null;
                
                // :CAUGHT:
                if (GameCameras.instance)
                {
                    GameCameras.instance.hudCanvas.LocateMyFSM("Slide Out").SendEvent("IN");
                }
            }

            if (VanishedHUD.Contains(prev.name))
            {
                GameManager.instance.StartCoroutine(CheckHud());
            }
        }

        private static void ModifyRadiance(On.PlayMakerFSM.orig_Start orig, PlayMakerFSM self)
        {
            orig(self);

            if (self is { name: "Absolute Radiance", FsmName: "Control" })
            {
                // Modify this action to leave p5 like the other doors
                self.GetAction<SetStaticVariable>("Ending Scene", 1).setValue.boolValue = false;
            }
        }

        private void RandomizeSequence(
            On.BossSequenceDoor.orig_Start orig,
            BossSequenceDoor self
        )
        {
            var seq = self.bossSequence;

            ref var bossScenes = ref Mirror.GetFieldRef<BossSequence, BossScene[]>(seq, "bossScenes");

            var scenes = bossScenes.ToList();

            while (true)
            {
                // Fisher–Yates shuffle
                for (int i = scenes.Count - 1; i > 0; i--)
                {
                    int x = _rand.Next(0, i + 1);
                    (scenes[i], scenes[x]) = (scenes[x], scenes[i]);
                }

                // Check the conditions
                bool f = InvalidFirst.Contains(scenes[0].sceneName) 
                      || InvalidLast.Contains(scenes[scenes.Count - 1].sceneName);
                
                if (!f)
                    break;
            }

            bossScenes = scenes.ToArray();

            orig(self);
        }

        public void Unload()
        {
            On.BossSequenceDoor.Start -= RandomizeSequence;
        }
    }
}