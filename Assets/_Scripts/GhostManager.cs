using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Ghost
{
    public string Nickname { get; set; }
    public Ghost(string nickname) => Nickname = nickname;
}

public class GhostManager : MonoBehaviour
{
    [SerializeField]
    private TMP_Text ghostText;
    public List<Ghost> ghosts = new();

    public void Start()
    {
        ghostText.text = "No Ghosts have joined yet.";
    }

    public void AddGhost(string Nickname)
    {
        ghosts.Add(new Ghost(Nickname));
        ghostText.text = GetGhostListString();
    }

    private string GetGhostListString()
    {
        return string.Join(", ", ghosts.ConvertAll(g => g.Nickname));
    }
}