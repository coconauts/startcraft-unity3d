@MenuItem ("Terrain/Heightmap From Texture")
 
static function ApplyHeightmap () {
	var heightmap : Texture2D = Selection.activeObject as Texture2D;
	if (heightmap == null) { 
		EditorUtility.DisplayDialog("No texture selected", "Please select a texture.", "Cancel"); 
		return; 
	}
	Undo.RegisterUndo (Terrain.activeTerrain.terrainData, "Heightmap From Texture");
 
	var terrain = Terrain.activeTerrain.terrainData;
	var w = heightmap.width;
	var h = heightmap.height;
	var w2 = terrain.heightmapWidth;
	var heightmapData = terrain.GetHeights(0, 0, w2, w2);
	var mapColors = heightmap.GetPixels();
	var map = new Color[w2 * w2];
 
	if (w2 != w || h != w) {
		// Resize using nearest-neighbor scaling if texture has no filtering
		if (heightmap.filterMode == FilterMode.Point) {
			var dx : float = parseFloat(w)/w2;
			var dy : float = parseFloat(h)/w2;
			for (y = 0; y < w2; y++) {
				if (y%20 == 0) {
					EditorUtility.DisplayProgressBar("Resize", "Calculating texture", Mathf.InverseLerp(0.0, w2, y));
				}
				var thisY = parseInt(dy*y)*w;
				var yw = y*w2;
				for (x = 0; x < w2; x++) {
					map[yw + x] = mapColors[thisY + dx*x];
				}
			}
		}
		// Otherwise resize using bilinear filtering
		else {
			var ratioX = 1.0/(parseFloat(w2)/(w-1));
			var ratioY = 1.0/(parseFloat(w2)/(h-1));
			for (y = 0; y < w2; y++) {
				if (y%20 == 0) {
					EditorUtility.DisplayProgressBar("Resize", "Calculating texture", Mathf.InverseLerp(0.0, w2, y));
				}
				var yy = Mathf.Floor(y*ratioY);
				var y1 = yy*w;
				var y2 = (yy+1)*w;
				yw = y*w2;
				for (x = 0; x < w2; x++) {
					var xx = Mathf.Floor(x*ratioX);
 
					var bl = mapColors[y1 + xx];
					var br = mapColors[y1 + xx+1]; 
					var tl = mapColors[y2 + xx];
					var tr = mapColors[y2 + xx+1];
 
					var xLerp = x*ratioX-xx;
					map[yw + x] = Color.Lerp(Color.Lerp(bl, br, xLerp), Color.Lerp(tl, tr, xLerp), y*ratioY-yy);
				}
			}
		}
		EditorUtility.ClearProgressBar();
	}
	else {
		// Use original if no resize is needed
		map = mapColors;
	}
 
	// Assign texture data to heightmap
	for (y = 0; y < w2; y++) {
		for (x = 0; x < w2; x++) {
			heightmapData[y,x] = map[y*w2+x].grayscale;
		}
	}
	terrain.SetHeights(0, 0, heightmapData);
}