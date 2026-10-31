using UnityEngine;

namespace GameLecture.Framework
{
    public class BaseScene : MonoBehaviour
    {
        [field: SerializeField] public string SceneId { get; protected set; }
    }

    public sealed class SceneManager : Singleton<SceneManager>
    {
        private BaseScene _currentScene;

        public BaseScene CurrentScene =>
            _currentScene != null ? _currentScene : _currentScene = FindAnyObjectByType<BaseScene>();

        public void LoadScene(string sceneName)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
            _currentScene = null;
        }

        public void LoadScene(int buildIndex)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(buildIndex);
            _currentScene = null;
        }
    }
}
