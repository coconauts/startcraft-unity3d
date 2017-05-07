//#define ASTARDEBUG		//"NavmeshController debug" Enables debugging lines
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Pathfinding;

/** CharacterController helper for use on navmeshes.
 * This character controller helper will clamp the desired movement to the navmesh before moving.\n
 * This results in that this character will not move out from the navmesh by it's own force by more than very small distances.\n
 * It can be used as a regular CharacterController, but the only move command you can use currently is SimpleMove.
 * \note A CharacterController component needs to be attached to the same GameObject for this script to work
 * \note It does only work on Navmesh based graphs (NavMeshGraph, RecastGraph)
 * \note It does not work very well with links in the graphs
 */
public class NavmeshController : MonoBehaviour {
}