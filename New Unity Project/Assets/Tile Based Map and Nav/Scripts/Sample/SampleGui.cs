// ====================================================================================================================
//
// Created by Leslie Young
// http://www.plyoung.com/ or http://plyoung.wordpress.com/
// ====================================================================================================================

using UnityEngine;
using System.Collections;

public class SampleGui : MonoBehaviour 
{

	private GameController game;
	private Rect winRect = new Rect(10f, 10f, 300f, 100f);

    void Start()
	{
		game = gameObject.GetComponent<GameController>();
	}

	void OnGUI()
	{
        if (game.allowInput)
		{
			winRect = GUILayout.Window(0, winRect, theWindow, "Game GUI");
		}
       /* GUI.Button(new Rect(550, 550, 100, 100), "A");
        GUI.Button(new Rect(650, 450, 100, 100), "W");
        GUI.Button(new Rect(650, 550, 100, 100), "S");
        GUI.Button(new Rect(750, 550, 100, 100), "D");*/
    }

	private void theWindow(int id)
	{
		/*GUILayout.Space(10f);
		game.useTurns = GUILayout.Toggle(game.useTurns, "USE TURNS");*/

        if (game.useTurns)
		{
			GUILayout.Space(10f);
            GUI.skin.button.fontSize = 80;
            if (GUILayout.Button("Change Turns")) game.ChangeTurn();

			/*GUILayout.Space(10f);
			GUILayout.Label(string.Format("Player {0}'s Turn", game.currPlayerTurn+1));*/
		}
	}
}
