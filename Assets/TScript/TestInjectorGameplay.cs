#if VCONTAINER
using _VuTH.Common.DI;
using _VuTH.Common.Log;
using _VuTH.Core.Camera;
using _VuTH.Core.GameCycle.ScreenFlow;
using _VuTH.Core.Persistant.DataPackage;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace TScript
{
    public class TestInjectorGameplay : MonoBehaviour, IVContainerConfigurator
    {
        private IDataPersistenceManager _dataPersistenceManager;
        private PlayerProfilePackage _playerProfile;
        
        public Button testButton;
        public TextMeshProUGUI levelText;

        [Inject]
        public void Construct(IDataPersistenceManager dataPersistence)
        {
            _dataPersistenceManager = dataPersistence;

            if (_dataPersistenceManager.TryGetPackage<PlayerProfilePackage>(out var playerProfile))
            {
                _playerProfile = playerProfile;
                this.Log($"Player profile loaded: {_playerProfile.PlayerName}");
            }
            else
            {
                this.LogError("Player profile package not found.");
            }
            
            Setup();
        }

        public void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(this);
        }

        private void Setup()
        {
            if (_playerProfile == null) return;
            
            testButton.onClick.AddListener(OnTestButtonClicked);
            _playerProfile.Level.Observable.Subscribe(OnLevelChanged).AddTo(this);
        }
        
        private void OnTestButtonClicked()
        {
            this.Log("Test button clicked!");
            _playerProfile.Level.Value++;
        }
        
        private void OnLevelChanged(int newLevel)
        {
            levelText.text = $"Level: {newLevel}";
        }
    }
}
#endif