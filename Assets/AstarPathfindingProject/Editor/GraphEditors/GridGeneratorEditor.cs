using UnityEngine;
using UnityEditor;
using Pathfinding.Serialization;

namespace Pathfinding {
	using Pathfinding.Util;

	[CustomGraphEditor(typeof(GridGraph), "Grid Graph")]
	public class GridGraphEditor : GraphEditor {
		[JsonMember]
		public bool locked = true;

		[JsonMember]
		public bool showExtra;

		GraphTransform savedTransform;
		Vector2 savedDimensions;
		float savedNodeSize;

		public bool isMouseDown;

		[JsonMember]
		public GridPivot pivot;

		/// <summary>Cached gui style</summary>
		static GUIStyle lockStyle;

		/// <summary>Cached gui style</summary>
		static GUIStyle gridPivotSelectBackground;

		/// <summary>Cached gui style</summary>
		static GUIStyle gridPivotSelectButton;

		static readonly float standardIsometric = 90-Mathf.Atan(1/Mathf.Sqrt(2))*Mathf.Rad2Deg;
		static readonly float standardDimetric = Mathf.Acos(1/2f)*Mathf.Rad2Deg;

		/// <summary>Rounds a vector's components to multiples of 0.5 (i.e 0.5, 1.0, 1.5, etc.) if very close to them</summary>
		public static Vector3 RoundVector3 (Vector3 v) {
			const int Multiplier = 2;

			if (Mathf.Abs(Multiplier*v.x - Mathf.Round(Multiplier*v.x)) < 0.001f) v.x = Mathf.Round(Multiplier*v.x)/Multiplier;
			if (Mathf.Abs(Multiplier*v.y - Mathf.Round(Multiplier*v.y)) < 0.001f) v.y = Mathf.Round(Multiplier*v.y)/Multiplier;
			if (Mathf.Abs(Multiplier*v.z - Mathf.Round(Multiplier*v.z)) < 0.001f) v.z = Mathf.Round(Multiplier*v.z)/Multiplier;
			return v;
		}

		public override void OnInspectorGUI (NavGraph target) {
			var graph = target as GridGraph;

			DrawFirstSection(graph);
			Separator();
			DrawMiddleSection(graph);
			Separator();
			DrawCollisionEditor(graph.collision);

			if (graph.collision.use2D) {
				if (Mathf.Abs(Vector3.Dot(Vector3.forward, Quaternion.Euler(graph.rotation) * Vector3.up)) < 0.9f) {
					EditorGUILayout.HelpBox("When using 2D physics it is recommended to rotate the graph so that it aligns with the 2D plane.", MessageType.Warning);
				}
			}

			Separator();
			DrawLastSection(graph);
		}

		bool IsHexagonal (GridGraph graph) {
			return Mathf.Approximately(graph.isometricAngle, standardIsometric) && graph.neighbours == NumNeighbours.Six && graph.uniformEdgeCosts;
		}

		bool IsIsometric (GridGraph graph) {
			if (graph.aspectRatio != 1) return true;
			if (IsHexagonal(graph)) return false;
			return graph.isometricAngle != 0;
		}

		bool IsAdvanced (GridGraph graph) {
			if (graph.inspectorGridMode == InspectorGridMode.Advanced) return true;
			// Weird configuration
			return (graph.neighbours == NumNeighbours.Six) != graph.uniformEdgeCosts;
		}

		InspectorGridMode DetermineGridType (GridGraph graph) {
			bool hex = IsHexagonal(graph);
			bool iso = IsIsometric(graph);
			bool adv = IsAdvanced(graph);

			if (adv || (hex && iso)) return InspectorGridMode.Advanced;
			if (hex) return InspectorGridMode.Hexagonal;
			if (iso) return InspectorGridMode.IsometricGrid;
			return graph.inspectorGridMode;
		}

		void DrawInspectorMode (GridGraph graph) {
			graph.inspectorGridMode = DetermineGridType(graph);
			var newMode = (InspectorGridMode)EditorGUILayout.EnumPopup("Shape", (System.Enum)graph.inspectorGridMode);
			if (newMode != graph.inspectorGridMode) {
				switch (newMode) {
				case InspectorGridMode.Grid:
					graph.isometricAngle = 0;
					graph.aspectRatio = 1;
					graph.uniformEdgeCosts = false;
					if (graph.neighbours == NumNeighbours.Six) graph.neighbours = NumNeighbours.Eight;
					break;
				case InspectorGridMode.Hexagonal:
					graph.isometricAngle = standardIsometric;
					graph.aspectRatio = 1;
					graph.uniformEdgeCosts = true;
					graph.neighbours = NumNeighbours.Six;
					break;
				case InspectorGridMode.IsometricGrid:
					graph.uniformEdgeCosts = false;
					if (graph.neighbours == NumNeighbours.Six) graph.neighbours = NumNeighbours.Eight;
					graph.isometricAngle = standardIsometric;
					break;
				case InspectorGridMode.Advanced:
				default:
					break;
				}
				graph.inspectorGridMode = newMode;
			}
		}

		static bool Is2D (GridGraph graph) {
			return Quaternion.Euler(graph.rotation) * Vector3.up == -Vector3.forward;
		}

		protected virtual void Draw2DMode (GridGraph graph) {
			EditorGUI.BeginChangeCheck();
			bool new2D = EditorGUILayout.Toggle(new GUIContent("2D"), Is2D(graph));
			if (EditorGUI.EndChangeCheck()) {
				graph.rotation = new2D ? new Vector3(graph.rotation.y - 90, 270, 90) : new Vector3(0, graph.rotation.x + 90, 0);
			}
		}

		GUIContent[] hexagonSizeContents = {
			new GUIContent("Hexagon Width", "Distance between two opposing sides on the hexagon"),
			new GUIContent("Hexagon Diameter", "Distance between two opposing vertices on the hexagon"),
			new GUIContent("Node Size", "Raw node size value, this doesn't correspond to anything particular on the hexagon."),
		};

		void DrawFirstSection (GridGraph graph) {
			float prevRatio = graph.aspectRatio;

			DrawInspectorMode(graph);

			Draw2DMode(graph);

			var normalizedPivotPoint = NormalizedPivotPoint(graph, pivot);
			var worldPoint = graph.CalculateTransform().Transform(normalizedPivotPoint);
			int newWidth, newDepth;

			DrawWidthDepthFields(graph, out newWidth, out newDepth);

			EditorGUI.BeginChangeCheck();
			float newNodeSize;
			if (graph.inspectorGridMode == InspectorGridMode.Hexagonal) {
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.BeginVertical();
				graph.inspectorHexagonSizeMode = (InspectorGridHexagonNodeSize)EditorGUILayout.EnumPopup(new GUIContent("Hexagon Dimension"), graph.inspectorHexagonSizeMode);
				float hexagonSize = GridGraph.ConvertNodeSizeToHexagonSize(graph.inspectorHexagonSizeMode, graph.nodeSize);
				hexagonSize = (float)System.Math.Round(hexagonSize, 5);
				newNodeSize = GridGraph.ConvertHexagonSizeToNodeSize(graph.inspectorHexagonSizeMode, EditorGUILayout.FloatField(hexagonSizeContents[(int)graph.inspectorHexagonSizeMode], hexagonSize));
				EditorGUILayout.EndVertical();
				if (graph.inspectorHexagonSizeMode != InspectorGridHexagonNodeSize.NodeSize) GUILayout.Box("", AstarPathEditor.astarSkin.FindStyle(graph.inspectorHexagonSizeMode == InspectorGridHexagonNodeSize.Diameter ? "HexagonDiameter" : "HexagonWidth"));
				EditorGUILayout.EndHorizontal();
			} else {
				newNodeSize = EditorGUILayout.FloatField(new GUIContent("Node size", "The size of a single node. The size is the side of the node square in world units"), graph.nodeSize);
			}
			bool nodeSizeChanged = EditorGUI.EndChangeCheck();

			newNodeSize = newNodeSize <= 0.01F ? 0.01F : newNodeSize;

			if (graph.inspectorGridMode == InspectorGridMode.IsometricGrid || graph.inspectorGridMode == InspectorGridMode.Advanced) {
				graph.aspectRatio = EditorGUILayout.FloatField(new GUIContent("Aspect Ratio", "Scaling of the nodes width/depth ratio. Good for isometric games"), graph.aspectRatio);

				DrawIsometricField(graph);
			}

			if ((nodeSizeChanged && locked) || (newWidth != graph.width || newDepth != graph.depth) || prevRatio != graph.aspectRatio) {
				graph.nodeSize = newNodeSize;
				graph.SetDimensions(newWidth, newDepth, newNodeSize);

				normalizedPivotPoint = NormalizedPivotPoint(graph, pivot);
				var newWorldPoint = graph.CalculateTransform().Transform(normalizedPivotPoint);
				// Move the center so that the pivot point stays at the same point in the world
				graph.center += worldPoint - newWorldPoint;
				graph.center = RoundVector3(graph.center);
				graph.UpdateTransform();
				AutoScan();
			}

			if ((nodeSizeChanged && !locked)) {
				graph.nodeSize = newNodeSize;
				graph.UpdateTransform();
			}

			DrawPositionField(graph);

			DrawRotationField(graph);
		}

		void DrawRotationField (GridGraph graph) {
			if (Is2D(graph)) {
				var right = Quaternion.Euler(graph.rotation) * Vector3.right;
				var angle = Mathf.Atan2(right.y, right.x) * Mathf.Rad2Deg;
				if (angle < 0) angle += 360;
				if (Mathf.Abs(angle - Mathf.Round(angle)) < 0.001f) angle = Mathf.Round(angle);
				EditorGUI.BeginChangeCheck();
				angle = EditorGUILayout.FloatField("Rotation", angle);
				if (EditorGUI.EndChangeCheck()) {
					graph.rotation = RoundVector3(new Vector3(-90 + angle, 270, 90));
				}
			} else {
				graph.rotation = RoundVector3(EditorGUILayout.Vector3Field("Rotation", graph.rotation));
			}
		}

		void DrawWidthDepthFields (GridGraph graph, out int newWidth, out int newDepth) {
			lockStyle = lockStyle ?? AstarPathEditor.astarSkin.FindStyle("GridSizeLock") ?? new GUIStyle();

			GUILayout.BeginHorizontal();
			GUILayout.BeginVertical();
			newWidth = EditorGUILayout.IntField(new GUIContent("Width (nodes)", "Width of the graph in nodes"), graph.width);
			newDepth = EditorGUILayout.IntField(new GUIContent("Depth (nodes)", "Depth (or height you might also call it) of the graph in nodes"), graph.depth);

			// Clamping will be done elsewhere as well
			// but this prevents negative widths from being converted to positive ones (since an absolute value will be taken)
			newWidth = Mathf.Max(newWidth, 1);
			newDepth = Mathf.Max(newDepth, 1);

			GUILayout.EndVertical();

			Rect lockRect = GUILayoutUtility.GetRect(lockStyle.fixedWidth, lockStyle.fixedHeight);

			GUILayout.EndHorizontal();

			// All the layouts mess up the margin to the next control, so add it manually
			GUILayout.Space(2);

			// Add a small offset to make it better centred around the controls
			lockRect.y += 3;
			lockRect.width = lockStyle.fixedWidth;
			lockRect.height = lockStyle.fixedHeight;
			lockRect.x += lockStyle.margin.left;
			lockRect.y += lockStyle.margin.top;

			locked = GUI.Toggle(lockRect, locked,
				new GUIContent("", "If the width and depth values are locked, " +
					"changing the node size will scale the grid while keeping the number of nodes consistent " +
					"instead of keeping the size the same and changing the number of nodes in the graph"), lockStyle);
		}

		void DrawIsometricField (GridGraph graph) {
			var isometricGUIContent = new GUIContent("Isometric Angle", "For an isometric 2D game, you can use this parameter to scale the graph correctly.\nIt can also be used to create a hexagonal grid.\nYou may want to rotate the graph 45 degrees around the Y axis to make it line up better.");
			var isometricOptions = new [] { new GUIContent("None (0°)"), new GUIContent("Isometric (≈54.74°)"), new GUIContent("Dimetric (60°)"), new GUIContent("Custom") };
			var isometricValues = new [] { 0f, standardIsometric, standardDimetric };
			var isometricOption = isometricValues.Length;

			for (int i = 0; i < isometricValues.Length; i++) {
				if (Mathf.Approximately(graph.isometricAngle, isometricValues[i])) {
					isometricOption = i;
				}
			}

			var prevIsometricOption = isometricOption;
			isometricOption = EditorGUILayout.IntPopup(isometricGUIContent, isometricOption, isometricOptions, new [] { 0, 1, 2, 3 });
			if (prevIsometricOption != isometricOption) {
				// Change to something that will not match the predefined values above
				graph.isometricAngle = 45;
			}

			if (isometricOption < isometricValues.Length) {
				graph.isometricAngle = isometricValues[isometricOption];
			} else {
				EditorGUI.indentLevel++;
				// Custom
				graph.isometricAngle = EditorGUILayout.FloatField(isometricGUIContent, graph.isometricAngle);
				EditorGUI.indentLevel--;
			}
		}

		static Vector3 NormalizedPivotPoint (GridGraph graph, GridPivot pivot) {
			switch (pivot) {
			case GridPivot.Center:
			default:
				return new Vector3(graph.width/2f, 0, graph.depth/2f);
			case GridPivot.TopLeft:
				return new Vector3(0, 0, graph.depth);
			case GridPivot.TopRight:
				return new Vector3(graph.width, 0, graph.depth);
			case GridPivot.BottomLeft:
				return new Vector3(0, 0, 0);
			case GridPivot.BottomRight:
				return new Vector3(graph.width, 0, 0);
			}
		}

		void DrawPositionField (GridGraph graph) {
			GUILayout.BeginHorizontal();
			var normalizedPivotPoint = NormalizedPivotPoint(graph, pivot);
			var worldPoint = RoundVector3(graph.CalculateTransform().Transform(normalizedPivotPoint));
			var newWorldPoint = EditorGUILayout.Vector3Field(ObjectNames.NicifyVariableName(pivot.ToString()), worldPoint);
			var delta = newWorldPoint - worldPoint;
			if (delta.magnitude > 0.001f) {
				graph.center += delta;
			}

			pivot = PivotPointSelector(pivot);
			GUILayout.EndHorizontal();
		}

		protected virtual void DrawMiddleSection (GridGraph graph) {
			DrawNeighbours(graph);
			DrawMaxClimb(graph);
			DrawMaxSlope(graph);
			DrawErosion(graph);
		}

		protected virtual void DrawCutCorners (GridGraph graph) {
			if (graph.inspectorGridMode == InspectorGridMode.Hexagonal) return;

			graph.cutCorners = EditorGUILayout.Toggle(new GUIContent("Cut Corners", "Enables or disables cutting corners. See docs for image example"), graph.cutCorners);
		}

		protected virtual void DrawNeighbours (GridGraph graph) {
			if (graph.inspectorGridMode == InspectorGridMode.Hexagonal) return;

			var neighboursGUIContent = new GUIContent("Connections", "Sets how many connections a node should have to it's neighbour nodes.");
			GUIContent[] neighbourOptions;
			if (graph.inspectorGridMode == InspectorGridMode.Advanced) {
				neighbourOptions = new [] { new GUIContent("Four"), new GUIContent("Eight"), new GUIContent("Six") };
			} else {
				neighbourOptions = new [] { new GUIContent("Four"), new GUIContent("Eight") };
			}
			graph.neighbours = (NumNeighbours)EditorGUILayout.Popup(neighboursGUIContent, (int)graph.neighbours, neighbourOptions);

			EditorGUI.indentLevel++;

			if (graph.neighbours == NumNeighbours.Eight) {
				DrawCutCorners(graph);
			}

			if (graph.neighbours == NumNeighbours.Six) {
				graph.uniformEdgeCosts = EditorGUILayout.Toggle(new GUIContent("Hexagon connection costs", "Tweak the edge costs in the graph to be more suitable for hexagon graphs"), graph.uniformEdgeCosts);
				EditorGUILayout.HelpBox("You can set all settings to make this a hexagonal graph by changing the 'Mode' field above", MessageType.None);
			} else {
				graph.uniformEdgeCosts = false;
			}

			EditorGUI.indentLevel--;
		}

		protected virtual void DrawMaxClimb (GridGraph graph) {
			if (!graph.collision.use2D) {
				graph.maxClimb = EditorGUILayout.FloatField(new GUIContent("Max Climb", "How high in world units, relative to the graph, should a climbable level be. A zero (0) indicates infinity"), graph.maxClimb);
				if (graph.maxClimb < 0) graph.maxClimb = 0;
			}
		}

		protected void DrawMaxSlope (GridGraph graph) {
			if (!graph.collision.use2D) {
				graph.maxSlope = EditorGUILayout.Slider(new GUIContent("Max Slope", "Sets the max slope in degrees for a point to be walkable. Only enabled if Height Testing is enabled."), graph.maxSlope, 0, 90F);
			}
		}

		protected void DrawErosion (GridGraph graph) {
			graph.erodeIterations = EditorGUILayout.IntField(new GUIContent("Erosion iterations", "Sets how many times the graph should be eroded. This adds extra margin to objects."), graph.erodeIterations);
			graph.erodeIterations = graph.erodeIterations < 0 ? 0 : (graph.erodeIterations > 16 ? 16 : graph.erodeIterations); //Clamp iterations to [0,16]

			if (graph.erodeIterations > 0) {
				EditorGUI.indentLevel++;
				graph.erosionUseTags = EditorGUILayout.Toggle(new GUIContent("Erosion Uses Tags", "Instead of making nodes unwalkable, " +
					"nodes will have their tag set to a value corresponding to their erosion level, " +
					"which is a quite good measurement of their distance to the closest wall.\nSee online documentation for more info."),
					graph.erosionUseTags);
				if (graph.erosionUseTags) {
					EditorGUI.indentLevel++;
					graph.erosionFirstTag = EditorGUILayoutx.TagField("First Tag", graph.erosionFirstTag, () => AstarPathEditor.EditTags());
					EditorGUI.indentLevel--;
				}
				EditorGUI.indentLevel--;
			}
		}

		void DrawLastSection (GridGraph graph) {
			GUILayout.BeginHorizontal();
			GUILayout.Space(18);
			graph.showMeshSurface = GUILayout.Toggle(graph.showMeshSurface, new GUIContent("Show surface", "Toggles gizmos for drawing the surface of the mesh"), EditorStyles.miniButtonLeft);
			graph.showMeshOutline = GUILayout.Toggle(graph.showMeshOutline, new GUIContent("Show outline", "Toggles gizmos for drawing an outline of the nodes"), EditorStyles.miniButtonMid);
			graph.showNodeConnections = GUILayout.Toggle(graph.showNodeConnections, new GUIContent("Show connections", "Toggles gizmos for drawing node connections"), EditorStyles.miniButtonRight);
			GUILayout.EndHorizontal();

			GUILayout.Label(new GUIContent("Advanced"), EditorStyles.boldLabel);

			DrawPenaltyModifications(graph);
			DrawJPS(graph);
		}

		void DrawPenaltyModifications (GridGraph graph) {
			showExtra = EditorGUILayout.Foldout(showExtra, "Penalty Modifications");

			if (showExtra) {
				EditorGUI.indentLevel += 2;

				graph.penaltyAngle = ToggleGroup(new GUIContent("Angle Penalty", "Adds a penalty based on the slope of the node"), graph.penaltyAngle);
				if (graph.penaltyAngle) {
					EditorGUI.indentLevel++;
					graph.penaltyAngleFactor = EditorGUILayout.FloatField(new GUIContent("Factor", "Scale of the penalty. A negative value should not be used"), graph.penaltyAngleFactor);
					graph.penaltyAnglePower = EditorGUILayout.Slider("Power", graph.penaltyAnglePower, 0.1f, 10f);
					EditorGUILayout.HelpBox("Applies penalty to nodes based on the angle of the hit surface during the Height Testing\nPenalty applied is: P=(1-cos(angle)^power)*factor.", MessageType.None);

					EditorGUI.indentLevel--;
				}

				graph.penaltyPosition = ToggleGroup("Position Penalty", graph.penaltyPosition);
				if (graph.penaltyPosition) {
					EditorGUI.indentLevel++;
					graph.penaltyPositionOffset = EditorGUILayout.FloatField("Offset", graph.penaltyPositionOffset);
					graph.penaltyPositionFactor = EditorGUILayout.FloatField("Factor", graph.penaltyPositionFactor);
					EditorGUILayout.HelpBox("Applies penalty to nodes based on their Y coordinate\nSampled in Int3 space, i.e it is multiplied with Int3.Precision first ("+Int3.Precision+")\n" +
						"Be very careful when using negative values since a negative penalty will underflow and instead get really high", MessageType.None);
					EditorGUI.indentLevel--;
				}

				GUI.enabled = false;
				ToggleGroup(new GUIContent("Use Texture", "A* Pathfinding Project Pro only feature\nThe Pro version can be bought on the A* Pathfinding Project homepage."), false);
				GUI.enabled = true;
				EditorGUI.indentLevel -= 2;
			}
		}

		protected virtual void DrawJPS (GridGraph graph) {
			// Jump point search is a pro only feature
		}

		/// <summary>Draws the inspector for a \link Pathfinding.GraphCollision GraphCollision class \endlink</summary>
		protected virtual void DrawCollisionEditor (GraphCollision collision) {
			collision = collision ?? new GraphCollision();

			DrawUse2DPhysics(collision);

			collision.collisionCheck = ToggleGroup("Collision testing", collision.collisionCheck);
			if (collision.collisionCheck) {
				string[] colliderOptions = collision.use2D ? new [] { "Circle", "Point" } : new [] { "Sphere", "Capsule", "Ray" };
				int[] colliderValues = collision.use2D ? new [] { 0, 2 } : new [] { 0, 1, 2 };
				// In 2D the Circle (Sphere) mode will replace both the Sphere and the Capsule modes
				// However make sure that the original value is still stored in the grid graph in case the user changes back to the 3D mode in the inspector.
				var tp = collision.type;
				if (tp == ColliderType.Capsule && collision.use2D) tp = ColliderType.Sphere;
				EditorGUI.BeginChangeCheck();
				tp = (ColliderType)EditorGUILayout.IntPopup("Collider type", (int)tp, colliderOptions, colliderValues);
				if (EditorGUI.EndChangeCheck()) collision.type = tp;

				// Only spheres and capsules have a diameter
				if (collision.type == ColliderType.Capsule || collision.type == ColliderType.Sphere) {
					collision.diameter = EditorGUILayout.FloatField(new GUIContent("Diameter", "Diameter of the capsule or sphere. 1 equals one node width"), collision.diameter);
				}

				if (!collision.use2D) {
					if (collision.type == ColliderType.Capsule || collision.type == ColliderType.Ray) {
						collision.height = EditorGUILayout.FloatField(new GUIContent("Height/Length", "Height of cylinder or length of ray in world units"), collision.height);
					}

					collision.collisionOffset = EditorGUILayout.FloatField(new GUIContent("Offset", "Offset upwards from the node. Can be used so that obstacles can be used as ground and at the same time as obstacles for lower positioned nodes"), collision.collisionOffset);
				}

				collision.mask = EditorGUILayoutx.LayerMaskField("Obstacle Layer Mask", collision.mask);
			}

			GUILayout.Space(2);

			if (collision.use2D) {
				EditorGUI.BeginDisabledGroup(collision.use2D);
				ToggleGroup("Height testing", false);
				EditorGUI.EndDisabledGroup();
			} else {
				collision.heightCheck = ToggleGroup("Height testing", collision.heightCheck);
				if (collision.heightCheck) {
					collision.fromHeight = EditorGUILayout.FloatField(new GUIContent("Ray length", "The height from which to check for ground"), collision.fromHeight);

					collision.heightMask = EditorGUILayoutx.LayerMaskField("Mask", collision.heightMask);

					collision.thickRaycast = EditorGUILayout.Toggle(new GUIContent("Thick Raycast", "Use a thick line instead of a thin line"), collision.thickRaycast);

					if (collision.thickRaycast) {
						EditorGUI.indentLevel++;
						collision.thickRaycastDiameter = EditorGUILayout.FloatField(new GUIContent("Diameter", "Diameter of the thick raycast"), collision.thickRaycastDiameter);
						EditorGUI.indentLevel--;
					}

					collision.unwalkableWhenNoGround = EditorGUILayout.Toggle(new GUIContent("Unwalkable when no ground", "Make nodes unwalkable when no ground was found with the height raycast. If height raycast is turned off, this doesn't affect anything"), collision.unwalkableWhenNoGround);
				}
			}
		}

		protected virtual void DrawUse2DPhysics (GraphCollision collision) {
			collision.use2D = EditorGUILayout.Toggle(new GUIContent("Use 2D Physics", "Use the Physics2D API for collision checking"), collision.use2D);
		}



		public static GridPivot PivotPointSelector (GridPivot pivot) {
			// Find required styles
			gridPivotSelectBackground = gridPivotSelectBackground ?? AstarPathEditor.astarSkin.FindStyle("GridPivotSelectBackground");
			gridPivotSelectButton = gridPivotSelectButton ?? AstarPathEditor.astarSkin.FindStyle("GridPivotSelectButton");

			Rect r = GUILayoutUtility.GetRect(19, 19, gridPivotSelectBackground);

			// I have no idea why... but this is required for it to work well
			r.y -= 14;

			r.width = 19;
			r.height = 19;

			if (gridPivotSelectBackground == null) {
				return pivot;
			}

			if (Event.current.type == EventType.Repaint) {
				gridPivotSelectBackground.Draw(r, false, false, false, false);
			}

			if (GUI.Toggle(new Rect(r.x, r.y, 7, 7), pivot == GridPivot.TopLeft, "", gridPivotSelectButton))
				pivot = GridPivot.TopLeft;

			if (GUI.Toggle(new Rect(r.x+12, r.y, 7, 7), pivot == GridPivot.TopRight, "", gridPivotSelectButton))
				pivot = GridPivot.TopRight;

			if (GUI.Toggle(new Rect(r.x+12, r.y+12, 7, 7), pivot == GridPivot.BottomRight, "", gridPivotSelectButton))
				pivot = GridPivot.BottomRight;

			if (GUI.Toggle(new Rect(r.x, r.y+12, 7, 7), pivot == GridPivot.BottomLeft, "", gridPivotSelectButton))
				pivot = GridPivot.BottomLeft;

			if (GUI.Toggle(new Rect(r.x+6, r.y+6, 7, 7), pivot == GridPivot.Center, "", gridPivotSelectButton))
				pivot = GridPivot.Center;

			return pivot;
		}

		static readonly Vector3[] handlePoints = new [] { new Vector3(0.0f, 0, 0.5f), new Vector3(1.0f, 0, 0.5f), new Vector3(0.5f, 0, 0.0f), new Vector3(0.5f, 0, 1.0f) };

		public override void OnSceneGUI (NavGraph target) {
			Event e = Event.current;

			var graph = target as GridGraph;

			graph.UpdateTransform();
			var currentTransform = graph.transform * Matrix4x4.Scale(new Vector3(graph.width, 1, graph.depth));

			if (e.type == EventType.MouseDown) {
				isMouseDown = true;
			} else if (e.type == EventType.MouseUp) {
				isMouseDown = false;
			}

			if (!isMouseDown) {
				savedTransform = currentTransform;
				savedDimensions = new Vector2(graph.width, graph.depth);
				savedNodeSize = graph.nodeSize;
			}

			Handles.matrix = Matrix4x4.identity;
			Handles.color = AstarColor.BoundsHandles;
#if UNITY_5_5_OR_NEWER
			Handles.CapFunction cap = Handles.CylinderHandleCap;
#else
			Handles.DrawCapFunction cap = Handles.CylinderCap;
#endif

			var center = currentTransform.Transform(new Vector3(0.5f, 0, 0.5f));
			if (Tools.current == Tool.Scale) {
				const float HandleScale = 0.1f;

				Vector3 mn = Vector3.zero;
				Vector3 mx = Vector3.zero;
				EditorGUI.BeginChangeCheck();
				for (int i = 0; i < handlePoints.Length; i++) {
					var ps = currentTransform.Transform(handlePoints[i]);
					Vector3 p = savedTransform.InverseTransform(Handles.Slider(ps, ps - center, HandleScale*HandleUtility.GetHandleSize(ps), cap, 0));

					// Snap to increments of whole nodes
					p.x = Mathf.Round(p.x * savedDimensions.x) / savedDimensions.x;
					p.z = Mathf.Round(p.z * savedDimensions.y) / savedDimensions.y;

					if (i == 0) {
						mn = mx = p;
					} else {
						mn = Vector3.Min(mn, p);
						mx = Vector3.Max(mx, p);
					}
				}

				if (EditorGUI.EndChangeCheck()) {
					graph.center = savedTransform.Transform((mn + mx) * 0.5f);
					graph.unclampedSize = Vector2.Scale(new Vector2(mx.x - mn.x, mx.z - mn.z), savedDimensions) * savedNodeSize;
				}
			} else if (Tools.current == Tool.Move) {
				EditorGUI.BeginChangeCheck();
				center = Handles.PositionHandle(graph.center, Tools.pivotRotation == PivotRotation.Global ? Quaternion.identity : Quaternion.Euler(graph.rotation));

				if (EditorGUI.EndChangeCheck() && Tools.viewTool != ViewTool.Orbit) {
					graph.center = center;
				}
			} else if (Tools.current == Tool.Rotate) {
				EditorGUI.BeginChangeCheck();
				var rot = Handles.RotationHandle(Quaternion.Euler(graph.rotation), graph.center);

				if (EditorGUI.EndChangeCheck() && Tools.viewTool != ViewTool.Orbit) {
					graph.rotation = rot.eulerAngles;
				}
			}

			Handles.matrix = Matrix4x4.identity;
		}

		public enum GridPivot {
			Center,
			TopLeft,
			TopRight,
			BottomLeft,
			BottomRight
		}
	}
}
