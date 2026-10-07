using System.Collections.Generic;
using UnityEngine;

// Which music plays where: stretches of the level along X, each with its own track. Lives at
// Resources/Soundtrack.asset (made automatically the first time Unity compiles this, see SoundtrackSetup).
// Edit the X ranges in the Inspector; the Console logs "Music: <section> (x ...)" whenever the section changes,
// which helps find good borders while playing.
[CreateAssetMenu(fileName = "Soundtrack", menuName = "Rowdy Dandy/Soundtrack Sections")]
public class SoundtrackConfig : ScriptableObject
{
    [System.Serializable]
    public class Section
    {
        public string name = "Section";
        [Tooltip("Scene this section belongs to. Empty = any scene.")]
        public string scene = "";
        [Tooltip("Rowdy's X from (inclusive) ...")]
        public float fromX = -100000f;
        [Tooltip("... to (exclusive)")]
        public float toX = 100000f;
        [Tooltip("Empty = silence here")]
        public AudioClip music;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [Tooltip("Checked top to bottom; the first section containing Rowdy's X wins.")]
    public List<Section> sections = new List<Section>();

    [Tooltip("Seconds to crossfade between tracks")]
    public float crossfade = 2.5f;

    [Tooltip("Rowdy must be this far past a border before the music changes (stops flip-flopping on the line)")]
    public float borderMargin = 2f;

    [Tooltip("Keep the current track going (instead of restarting it) when dying / reloading in the same section")]
    public bool keepPlayingOnReload = true;
}
