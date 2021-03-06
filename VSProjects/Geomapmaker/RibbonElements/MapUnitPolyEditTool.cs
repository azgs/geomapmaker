﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Geomapmaker.Models;

namespace Geomapmaker {
	internal class MapUnitPolyEditTool : MapTool {
		public MapUnitPolyEditTool() {
			GeomapmakerModule.EditMapUnitPolyTool = this;
			IsSketchTool = true;
			SketchType = SketchGeometryType.Point;
			SketchOutputMode = SketchOutputMode.Map;
			UseSelection = false;
		}

		public void Clear() {
			ClearSketchAsync();
			UseSelection = false;
			SketchType = SketchGeometryType.Point;
		}

		protected override Task OnToolActivateAsync(bool active) {
			AddEditMapUnitPolysDockPaneViewModel.Show();
			GeomapmakerModule.MapUnitPolysVM.Heading = "Edit Map Unit Poly";
			return base.OnToolActivateAsync(active);
		}
		protected override Task OnToolDeactivateAsync(bool hasMapViewChanged) {
			AddEditMapUnitPolysDockPaneViewModel.Hide();
			return base.OnToolDeactivateAsync(hasMapViewChanged);
		}

		protected override async Task<bool> OnSketchCompleteAsync(Geometry geometry) {

			var mv = MapView.Active;
			var identifyResult = await QueuedTask.Run(() => {

				//mv.SelectFeatures(geometry);

				if (UseSelection == false) {
					//User just selected a poly to edit. Populate form and set up to allow geom editing

					// Get the features that intersect the sketch geometry.
					var features = mv.GetFeatures(geometry);
					//Only interested in MapUnitPolys
					var mapUnitFeatures = features.Where(x => x.Key.Name == "MapUnitPolys");
					if (mapUnitFeatures.Count() > 0) {
						//Get map unit objectid
						//TODO: I am only pulling the first from the list. Might need to present some sort of selector to the user. 
						if (mapUnitFeatures.First().Value.Count() > 0) {
							var mapUnitPolyID = mapUnitFeatures.First().Value.First();

							//Using the objectid, get the map unit record from the database
							using (Geodatabase geodatabase = new Geodatabase(DataHelper.connectionProperties)) {
								using (Table mapUnitPolysTable = geodatabase.OpenDataset<Table>("MapUnitPolys")) {
									QueryFilter queryFilter = new QueryFilter {
										WhereClause = "objectid = " + mapUnitPolyID
									};
									var mapUnitPoly = new MapUnitPoly();
									using (RowCursor rowCursor = mapUnitPolysTable.Search(queryFilter, false)) {
										if (rowCursor.MoveNext()) {
											using (Row row = rowCursor.Current) {
												mapUnitPoly.ID = Int32.Parse(Convert.ToString(row["objectid"]));
												mapUnitPoly.MapUnit = DataHelper.MapUnits.Where(x => x.MU == Convert.ToString(row["mapunit"])).First();
												mapUnitPoly.Label = Convert.ToString(row["label"]);
												mapUnitPoly.Notes = Convert.ToString(row["notes"]);
												mapUnitPoly.Symbol = Convert.ToString(row["symbol"]);
												mapUnitPoly.IdentityConfidence = Convert.ToString(row["identityconfidence"]);
												mapUnitPoly.DataSource = Convert.ToString(row["datasourceid"]);
												mapUnitPoly.Shape = (Geometry)row["shape"]; //TODO: no idea
											}
										}

									}

									// Use the map unit to populate the view model for the form
									GeomapmakerModule.MapUnitPolysVM.SelectedMapUnitPoly = mapUnitPoly;
									GeomapmakerModule.MapUnitPolysVM.SelectedMapUnit = mapUnitPoly.MapUnit;
									//TODO: The line below forces a refresh on the object the UI is bound to for shape. But other than that,
									//it's redundant. There should be a better way to handle this.
									GeomapmakerModule.MapUnitPolysVM.Shape = GeomapmakerModule.MapUnitPolysVM.SelectedMapUnitPoly.Shape;

									//UseSelection = true;
									//TODO: I'm not sure what we want to be able to do with the polygon at this point.
									//But I need the outline of the polygon to display on the map.
									//Using SketchMode.Midpoint at least makes that polygon sketch stay calm 
									//(omit that line and you'll see what I mean).   
									SketchType = SketchGeometryType.Polygon;
									//SketchMode = SketchMode.Midpoint;
									ContextToolbarID = "";
									SetCurrentSketchAsync(mapUnitPoly.Shape);

								}
							}
						}
					}
					return true;
				} else {
					//User has just modified the geometery, stick it on the form
					GeomapmakerModule.MapUnitPolysVM.Shape = geometry;
					SetCurrentSketchAsync(geometry);
					//UseSelection = false;
					//SketchType = SketchGeometryType.Point;
					//TODO: more stuff?
					return true;
				}
			});
			//MessageBox.Show(identifyResult);
			return true;
		}
	}
}
