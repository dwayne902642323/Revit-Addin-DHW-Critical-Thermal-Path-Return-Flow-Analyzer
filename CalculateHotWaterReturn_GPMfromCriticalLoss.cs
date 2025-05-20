// Revit ExternalCommand: Hot Water Critical Heat Path + Return Flow Analyzer

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;

namespace RevitHotWaterReturnFlow
{
    [Transaction(TransactionMode.Manual)]
    public class CalculateHotWaterReturn_GPMfromCriticalLoss : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Step 1: Validate user selection
            var selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds.Count != 1)
            {
                TaskDialog.Show("Error", "Please select a single Domestic Hot Water pipe to analyze.");
                return Result.Failed;
            }

            Pipe startPipe = doc.GetElement(selectedIds.First()) as Pipe;
            if (startPipe == null || startPipe.PipeSystemType != PipeSystemType.DomesticHotWater)
            {
                TaskDialog.Show("Error", "Selected element must be a Domestic Hot Water pipe.");
                return Result.Failed;
            }

            // Step 2: Constants (editable in future UI)
            double supplyTempF = 120;
            double ambientTempF = 75;
            double insulationThicknessIn = 1.0;
            double insulationRValue = 4.0;
            double allowableDeltaT = 10;

            // Step 3: Collect all DHW pipes
            var allDHWpipes = new FilteredElementCollector(doc)
                .OfClass(typeof(Pipe))
                .Cast<Pipe>()
                .Where(p => p.PipeSystemType == PipeSystemType.DomesticHotWater)
                .ToDictionary(p => p.Id);

            // Graph and metadata setup
            Dictionary<ElementId, List<ElementId>> pipeGraph = new();
            Dictionary<ElementId, double> pipeLosses = new();
            Dictionary<ElementId, ElementId> parentMap = new();
            HashSet<ElementId> visited = new();
            Queue<ElementId> queue = new();
            queue.Enqueue(startPipe.Id);

            // Step 4: Traverse and calculate loss
            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                if (!visited.Add(currentId)) continue;
                if (!allDHWpipes.TryGetValue(currentId, out Pipe pipe)) continue;

                double diameterIn = pipe.Diameter * 12.0;
                double lengthFt = (pipe.Location as LocationCurve)?.Curve?.Length ?? 0.0;

                double qLoss = HeatLossCalculator.CalculatePipeSegmentHeatLoss(
                    diameterIn, insulationThicknessIn, insulationRValue,
                    lengthFt, supplyTempF, ambientTempF);

                pipeLosses[currentId] = qLoss;

                var connected = new List<ElementId>();
                foreach (Connector conn in pipe.ConnectorManager?.Connectors ?? new ConnectorSet())
                {
                    if (conn.ConnectorType != ConnectorType.End) continue;
                    foreach (Connector refConn in conn.AllRefs)
                    {
                        if (refConn.Owner is Pipe neighbor && neighbor.Id != pipe.Id &&
                            neighbor.PipeSystemType == PipeSystemType.DomesticHotWater)
                        {
                            connected.Add(neighbor.Id);
                            if (!visited.Contains(neighbor.Id))
                            {
                                queue.Enqueue(neighbor.Id);
                                if (!parentMap.ContainsKey(neighbor.Id))
                                    parentMap[neighbor.Id] = currentId;
                            }
                        }
                    }
                }
                pipeGraph[currentId] = connected.Distinct().ToList();
            }

            // Step 5: DFS with memoization to find max heat loss path
            Dictionary<ElementId, double> memo = new();
            double maxTotalLoss = 0;
            ElementId endPipeId = null;

            foreach (var pipeId in pipeLosses.Keys)
            {
                double loss = Traverse(pipeId, pipeGraph, pipeLosses, memo);
                if (loss > maxTotalLoss)
                {
                    maxTotalLoss = loss;
                    endPipeId = pipeId;
                }
            }

            // Step 6: Reconstruct critical path from parent map
            List<ElementId> criticalPath = new();
            var current = endPipeId;
            while (current != null && pipeLosses.ContainsKey(current))
            {
                criticalPath.Insert(0, current);
                parentMap.TryGetValue(current, out current);
            }

            // Step 7: Solve for return flow
            double cp = 1.0;  // BTU/lb·°F
            double rho = 8.34; // lb/gal
            double mDot = maxTotalLoss / (cp * allowableDeltaT); // lb/hr
            double gpm = mDot / (rho * 60);

            // Step 8: Highlight path in red
            OverrideGraphicSettings ogs = new OverrideGraphicSettings();
            ogs.SetProjectionLineColor(new Color(255, 0, 0));
            using (Transaction tx = new Transaction(doc, "Highlight Critical DHW Path"))
            {
                tx.Start();
                foreach (var id in criticalPath)
                    doc.ActiveView.SetElementOverrides(id, ogs);
                tx.Commit();
            }

            // Step 9: Export to CSV
            List<string> csvLines = new() { "PipeId,HeatLoss_BTUhr" };
            foreach (var kvp in pipeLosses)
                csvLines.Add($"{kvp.Key.IntegerValue},{kvp.Value:F2}");
            csvLines.Add($"Total_Critical_Path_HeatLoss_BTUhr,{maxTotalLoss:F2}");
            csvLines.Add($"Required_Return_Flow_GPM,{gpm:F2}");
            File.WriteAllLines("C:\\RevitOutput\\HotWater_CriticalHeatPath.csv", csvLines);

            // Step 10: Show results
            TaskDialog.Show("Hot Water Return Flow",
                $"Max heat loss: {maxTotalLoss:F1} BTU/hr\nRequired return flow: {gpm:F2} GPM\nCSV saved to: C:\\RevitOutput\\HotWater_CriticalHeatPath.csv");

            return Result.Succeeded;
        }

        private double Traverse(ElementId currentId,
            Dictionary<ElementId, List<ElementId>> graph,
            Dictionary<ElementId, double> losses,
            Dictionary<ElementId, double> memo)
        {
            if (memo.ContainsKey(currentId)) return memo[currentId];

            double localLoss = losses.GetValueOrDefault(currentId, 0);
            double maxBranchLoss = 0;

            if (graph.TryGetValue(currentId, out var neighbors))
            {
                foreach (var next in neighbors)
                {
                    double branchLoss = Traverse(next, graph, losses, memo);
                    if (branchLoss > maxBranchLoss)
                        maxBranchLoss = branchLoss;
                }
            }

            double total = localLoss + maxBranchLoss;
            memo[currentId] = total;
            return total;
        }
    }

    public static class HeatLossCalculator
    {
        public static double CalculatePipeSegmentHeatLoss(
            double diameterIn,
            double insulationIn,
            double rValue,
            double lengthFt,
            double tempSupply,
            double tempAmbient)
        {
            double r1 = (diameterIn / 2.0) / 12.0;
            double r2 = ((diameterIn + 2 * insulationIn) / 2.0) / 12.0;

            if (r2 <= r1 || rValue <= 0) return 0;

            double kEff = Math.Log(r2 / r1) / (2 * Math.PI * rValue);
            return (2 * Math.PI * lengthFt * kEff * (tempSupply - tempAmbient)) / Math.Log(r2 / r1);
        }
    }
}
