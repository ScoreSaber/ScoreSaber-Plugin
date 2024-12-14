﻿using ScoreSaber.Core.ReplaySystem.UI;
using ScoreSaber.Core.Services;
using ScoreSaber.Core.Utils;
using Zenject;

namespace ScoreSaber.Core.ReplaySystem.Installers
{
    internal class ImberInstaller : Installer
    {
        public override void InstallBindings() {

            if (Plugin.ReplayState.IsPlaybackEnabled && !Plugin.ReplayState.IsLegacyReplay) {
                Container.Bind<VRControllerAccessor>().AsSingle();
                Container.Bind<TweeningUtils>().AsSingle();
                Container.BindInterfacesAndSelfTo<DesktopMainImberPanelView>().FromNewComponentAsViewController().AsSingle();
                Container.BindInterfacesTo<ImberManager>().AsSingle();
                Container.BindInterfacesAndSelfTo<ImberScrubber>().AsSingle();
                Container.BindInterfacesAndSelfTo<ImberSpecsReporter>().AsSingle();
                Container.BindInterfacesAndSelfTo<ImberUIPositionController>().AsSingle();
                Container.Bind<MainImberPanelView>().FromNewComponentAsViewController().AsSingle();
                Container.Bind(typeof(ITickable), typeof(SpectateAreaController)).To<SpectateAreaController>().AsSingle();
            }
        }
    }
}