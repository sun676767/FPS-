using System.Collections.Generic;
using UnityEngine;

namespace GameLecture.Framework
{
    public sealed class SoundManager : Singleton<SoundManager>
    {
        private readonly Dictionary<string, AudioSource> _channels = new();
        private readonly Dictionary<string, AudioClip> _clips = new();
        private Transform _root;

        public AudioSource GetChannel(string channel, bool loop = false)
        {
            if (_channels.TryGetValue(channel, out var source))
                return source;

            var go = new GameObject(channel);
            go.transform.SetParent(FrameworkUtils.GetOrCreateRoot(ref _root, "@SoundRoot"));
            source = go.AddComponent<AudioSource>();
            source.loop = loop;
            _channels.Add(channel, source);
            return source;
        }

        public void Play2D(string channel, string clipKey, bool loop = false, float pitch = 1f)
        {
            var clip = GetClip(clipKey);
            if (clip != null)
                Play2D(channel, clip, loop, pitch);
        }

        public void Play2D(string channel, AudioClip clip, bool loop = false, float pitch = 1f)
        {
            var source = GetChannel(channel, loop);
            source.pitch = pitch;
            source.loop = loop;
            if (loop)
            {
                source.clip = clip;
                source.Play();
            }
            else
            {
                source.PlayOneShot(clip);
            }
        }

        public void Play3D(AudioClip clip, GameObject target, float minDistance = 1f,
            float maxDistance = 20f, float pitch = 1f)
        {
            var source = target.GetOrAddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 1f;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.pitch = pitch;
            source.Play();
        }

        public void Stop(string channel)
        {
            if (_channels.TryGetValue(channel, out var source))
                source.Stop();
        }

        public void Clear()
        {
            foreach (var source in _channels.Values)
                source.Stop();
            _clips.Clear();
        }

        private AudioClip GetClip(string key)
        {
            if (!_clips.TryGetValue(key, out var clip))
                _clips.Add(key, clip = ResourceManager.Instance.Get<AudioClip>(key));
            return clip;
        }
    }
}
