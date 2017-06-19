// ====================================================================================================================
//
// Created by Leslie Young
// http://www.plyoung.com/ or http://plyoung.wordpress.com/
// ====================================================================================================================

#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TileNode: MonoBehaviour
{
	// ====================================================================================================================
	#region inspector properties

	public MapNav parent = null;			// mapnav that contains this tile
	public int idx = 0;						// unique id of tile

	// tile levels that this tile supports. this will determine what units can move onto this tile
	public TileType tileTypeMask = 0;

	#endregion
	// ====================================================================================================================
	#region vars

	public enum TileType
	{
		// you can add/remove as you like, just make sure to use 2 to the power for values to follow
		Normal		= 0x1,	// 1 normal/land tile (land and air units can move over it)
		Air			= 0x2,	// 2 only air units can move over these tiles
		Water		= 0x4,	// 4 water tile, only water and air units can move over it
		Wall		= 0x8,	// 8 a wall/obstacle, nothing can move onto this tile
		//etc		= 0x10, // 16
		//etc		= 0x20, // 32
		//etc		= 0x40, // 64
		//etc		= 0x80, // 128
		//etc		= 0x100,// 256
		//etc		= 0x200,// 512
		//etc		= 0x400,
		//etc		= 0x800,
		//etc		= 0x1000,
		//etc		= 0x2000,
		//etc		= 0x4000,
		//etc		= 0x8000,
		//etc		= 0x10000,
	}

#if UNITY_EDITOR
	// these are used with the Gizmo renering when MapNav.gizmoColorCoding=true to help identify TileTypes.
	// each colour here will be an index into TileType, so 1st one is Normal(1), 2nd is Air(2), 3rd Water(4), etc
	public static Color[] GizmoColourCoding = 
	{
		Color.green,	// normal
		Color.cyan,		// air
		Color.blue,		// water
		Color.grey,		// wall
		//Color.yellow,
		// etc
		// etc
	};
#endif

	// neighbouring nodes, the nodes this one is linked with. linking happens in MapNav.LinkNodes()
	public TileNode[] nodeLinks { get; set; }

	// the unit(s) that are sitting on this tile. More than one NaviUnit can occupy a tile depending on
	// the units' tileLevel. Units with the same tielLevel can't occupy the same 'level' on a tile.
	// Example, you could have a TileType.Normal type unit and TileType.Air type unit on the same tile
	// since a flying unit can hover over a land unit
	public List<NaviUnit> units { get; set; }

	public bool IsVisible { get; set; }

	// ====================================================================================================================

	public TNEMovementModifier movesMod { get; private set; }			// cache of the component
	public TNELinksOnOffSwitch linkOnOffSwitch { get; private set; }	// cache of the component

	#endregion
	// ====================================================================================================================
	#region pub

	void Start()
	{
		units = new List<NaviUnit>();

		linkOnOffSwitch = gameObject.GetComponent<TNELinksOnOffSwitch>();
		movesMod = gameObject.GetComponent<TNEMovementModifier>();
	}

	/// <summary>activates an animation on the tile if mouseOver is true, else the animation is stoped</summary>
	public void OnHover(bool mouseOver)
	{
		if (!this.GetComponent<Animation>()) return;
		if (mouseOver)
		{
			this.GetComponent<Animation>().Play();
		}
		else
		{
			this.GetComponent<Animation>().Stop();

			// reset the material's alpha to full since the animation that was played messed around with it
			// please change this according to the kind of animation to used if you changed from the default
			// Map&Nav aniamtion which is to change the alpha value of the tilenode
			Color c = this.GetComponent<Renderer>().material.color;
			c.a = 1f;
			this.GetComponent<Renderer>().material.color = c;
		}
	}

	/// <summary>
	/// Show and make this tile active (an active tile can be clicked on)
	/// Pass false to deactivate and hide the tile
	/// </summary>
	public void Show(bool doShow)
	{
		this.IsVisible = doShow;
		this.GetComponent<Renderer>().enabled = doShow;
		this.GetComponent<Collider>().enabled = doShow;
	}

	public void Show()
	{
		Show(true);
	}

	public void Hide()
	{
		Show(false);
	}

	/// <summary>Show/Hide all the neighbours of this tile in a certain range around it</summary>
	public void ShowNeighbours(int radius, bool show)
	{
		this._ShowNeighboursRecursive(radius, show, 0, false, false);
	}

	/// <summary>
	/// Show tiles that include certain layer in mask and do not have any unit standing in that spot
	/// Good for showing nodes that a unit may move over.
	/// </summary>
	public void ShowNeighbours(int radius, TileNode.TileType validNodesLayer)
	{
		this._ShowNeighboursRecursive(radius, true, validNodesLayer, false, false);
	}

	/// <summary>
	/// Show tiles that include certain layer in mask and do not have any unit standing in that spot
	/// Good for showing nodes that a unit may move over.
	/// </summary>
	public void ShowNeighbours(int radius, TileNode.TileType validNodesLayer, bool checkMoveMod, bool checkOnOffSwitch)
	{
		this._ShowNeighboursRecursive(radius, true, validNodesLayer, checkMoveMod, checkOnOffSwitch);
	}

	/// <summary>return the unit that is occupying the level on this tile</summary>
	public NaviUnit GetUnitInLevel(TileType level)
	{
		foreach (NaviUnit u in units)
		{
			if (u.tileLevel == level) return u;
		}
		return null;
	}

	/// <summary>Checks if the tergate node is in radius range from this node</summary>
	public bool IsInRange(TileNode targetNode, int radius)
	{
		radius--;
		if (radius < 0) return false;
		foreach (TileNode node in nodeLinks)
		{
			if (node != null)
			{
				if (node == targetNode) return true;
				if (node.IsInRange(targetNode, radius)) return true;
			}
		}
		return false;
	}

	/// <summary>Unlink with specified node</summary>
	public void UnlinkWithNode(TileNode node)
	{
		if (nodeLinks != null && node != null)
		{
			for (int i=0; i<nodeLinks.Length; i++)
			{
				if (nodeLinks[i] == node)
				{
					nodeLinks[i] = null;
					break;
				}
			}
		}

	}

	/// <summary>This will unlink the node with its neighbours (and units if linked). Should be called if a node is to be deleted</summary>
	public void Unlink()
	{
		if (units != null)
		{
			foreach (NaviUnit u in units) u.node = null;
		}

		if (nodeLinks != null)
		{
			foreach (TileNode n in nodeLinks)
			{
				if (n!=null) n.UnlinkWithNode(this);
			}
		}

	}

	/// <summary>returns true if withNode is a direct neighbour of this node (linked in nodeLinks) </summary>
	public bool IsDirectNeighbour(TileNode withNode)
	{
		foreach (TileNode n in nodeLinks)
		{
			if (n == withNode) return true;
		}
		return false;
	}

	/// <summary>Checks if link with the target node is on or off. See TNELinksOnOffSwitch</summary>
	public bool LinkIsOnWith(TileNode withNode)
	{
		// first check if provided link is actually a neighbour of this one
		if (!IsDirectNeighbour(withNode)) return false;

		int state = -1;

		// check if the link with the target is on or off
		if (linkOnOffSwitch != null)
		{	// check if link is on with neighbour
			state = linkOnOffSwitch.LinkIsOn(withNode);
		}

		// not found? check if the neighbour is on carrying the link info
		if (state == -1)
		{
			if (withNode.linkOnOffSwitch != null)
			{	// check if link is on from neighbour's side
				state = withNode.linkOnOffSwitch.LinkIsOn(this);
			}
		}

		// default is true, only return false if specifically told that link is off 
		return (state == 0?false:true);
	}

	/// <summary>
	/// Set the link withNode on or off. Returns false if operation could not be completed, 
	/// for example withNode was not a neighbour of this node
	/// </summary>
	public bool SetLinkState(TileNode withNode, bool on)
	{
		if (!IsDirectNeighbour(withNode)) return false;

		bool needToAdd = false;
		if (linkOnOffSwitch != null)
		{
			linkOnOffSwitch.SetLinkStateWith(withNode, on);
			needToAdd = false;
		}
		
		// check if neighbour might carry a link back to this node, and update
		if (withNode.linkOnOffSwitch != null)
		{
			withNode.linkOnOffSwitch.SetLinkStateWith(this, on);
			needToAdd = false;
		}

		// if none had link info, then add it now
		if (needToAdd)
		{
			linkOnOffSwitch = gameObject.AddComponent<TNELinksOnOffSwitch>();
			linkOnOffSwitch.SetLinkStateWith(withNode, on);
		}

		return true;
	}

	/// <summary>
	/// set the link state with all that are linked, will not modify any neighbours that have no switched on them
	/// </summary>
	public void SetLinkStateWithAll(bool on)
	{
		if (linkOnOffSwitch==null) return;

		foreach (TNELinksOnOffSwitch.LinkState ls in linkOnOffSwitch.linkStates)
		{
			ls.isOn = on;

			if (ls.neighbour.linkOnOffSwitch = null)
			{
				foreach (TNELinksOnOffSwitch.LinkState lsb in ls.neighbour.linkOnOffSwitch.linkStates)
				{
					if (lsb.neighbour == this)
					{
						lsb.isOn = on;
						break;
					}
				}
			}
		}
	}

	#endregion
	// ====================================================================================================================
	#region workers/helper

	/// <summary>Helper for ShowNeighbours.. functions</summary>
	public void _ShowNeighboursRecursive(int radius, bool show, TileNode.TileType validNodesLayer, bool inclMoveMod, bool incLinkCheck)
	{
		radius--;
		if (radius < 0) return;
		List<TileNode> skipList = new List<TileNode>();
		foreach (TileNode node in this.nodeLinks)
		{
			if (node == null) continue;

			if (show == true)
			{
				if (validNodesLayer > 0)
				{
					// if validNodesLayer given, then check if this is a valid node according to it mask
					if ((node.tileTypeMask & validNodesLayer) != validNodesLayer) continue;

					// also check if another unit is occupying the same layer, which makes this node invalid
					if (node.GetUnitInLevel(validNodesLayer)) continue;
				}

				// check if movement mod applies
				if (inclMoveMod && node.movesMod != null)
				{
					int r = radius;
					foreach (TNEMovementModifier.MovementInfo m in node.movesMod.moveInfos)
					{
						if (m.tileType == validNodesLayer) r -= m.movesModifier;
					}
					if (r < 0) continue;
					if (r <= 0) skipList.Add(node);
				}

				// check if link possibly off
				if (incLinkCheck)
				{
				    if (!this.LinkIsOnWith(node))
				    {
				        skipList.Add(node);
				        continue;
				    }
				}
			}

			node.Show(show);
		}

		foreach (TileNode node in this.nodeLinks)
		{
			if (node == null) continue;

			if (show == true)
			{
				if (validNodesLayer > 0)
				{
					if ((node.tileTypeMask & validNodesLayer) != validNodesLayer) continue;
					if (node.GetUnitInLevel(validNodesLayer)) continue;
				}

				if (skipList.Contains(node)) continue;
			}

			node._ShowNeighboursRecursive(radius, show, validNodesLayer, inclMoveMod, incLinkCheck);
		}
	}

	// ====================================================================================================================
	// navigation system helpers

	// used during path calculations
	private TileNode pathParent = null;

	public void SetPathParent(TileNode parent, int g, int h)
	{
		pathParent = parent;
		PathG = g;
		PathH = h;
		PathF = g + h;
	}

	public TileNode PathParent { get { return pathParent; } }
	public int PathF { get; private set; }
	public int PathG { get; private set; }
	public int PathH { get; private set; }

	#endregion
	// ====================================================================================================================
	#region gizmos

#if UNITY_EDITOR
	private static Vector3 GizmoSize = new Vector3(0.2f, 0.1f, 0.2f);
    [DrawGizmo (GizmoType.NotInSelectionHierarchy | GizmoType.Pickable)]
    static void RenderTileNodeGizmo(TileNode node, GizmoType gizmoType) 
	{
		if (node.parent == null) return;
        Vector3 position = node.transform.position;

		// draw cube(s) for node
		if (node.parent.gizmoDrawNodes || ((gizmoType & GizmoType.Selected) != 0))
		{
			if ((gizmoType & GizmoType.Selected) != 0)
			{
				Gizmos.color = Color.red;
				Gizmos.DrawCube(position, TileNode.GizmoSize);
			}

			else if (node.parent.gizmoColorCoding)
			{
				bool drawn = false;
				Vector3 offs = Vector3.zero;
				for (int i = 0; i < TileNode.GizmoColourCoding.Length; i++)
				{
					int t = (int)Mathf.Pow(2, i);
					if ( ((int)node.tileTypeMask & t) == t)
					{
						Gizmos.color = TileNode.GizmoColourCoding[i];
						Gizmos.DrawCube(position + offs, TileNode.GizmoSize);
						offs.y += TileNode.GizmoSize.y;
						drawn = true;
					}
				}

				if (!drawn)
				{
					Gizmos.color = Color.black;
					Gizmos.DrawCube(position, TileNode.GizmoSize);
				}
			}

			else
			{
				Gizmos.color = Color.black;
				Gizmos.DrawCube(position, TileNode.GizmoSize);
			}
		}

		// draw the node links
		if (node.nodeLinks != null && (node.parent.gizmoDrawLinks || (gizmoType & GizmoType.Selected) != 0))
		{			
			TNELinksOnOffSwitch linkSwitch = node.gameObject.GetComponent<TNELinksOnOffSwitch>();
			foreach (TileNode n in node.nodeLinks)
		    {				
		        if (n != null)
		        {
					Gizmos.color = Color.blue;

					if (linkSwitch != null)
					{	// check if lik is on with neighbour
						if (linkSwitch.LinkIsOn(n)==0) Gizmos.color = Color.red;
					}

					TNELinksOnOffSwitch nls = n.gameObject.GetComponent<TNELinksOnOffSwitch>();
					if (nls != null)
					{	// check if neighbour's link with me is on
						if (nls.LinkIsOn(node)==0) Gizmos.color = Color.red;
					}

					Gizmos.DrawLine(position, n.transform.position);
		        }
		    }
		}

	}
#endif

	#endregion
	// ====================================================================================================================
}
