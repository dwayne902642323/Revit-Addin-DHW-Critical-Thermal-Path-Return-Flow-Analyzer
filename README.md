Hot Water Return Analyzer – Revit Add-In

Author: Derek Eubanks, PE | Georgia Tech MSCS – Machine LearningLanguage: C# (.NET)Platform: Autodesk Revit 2024+API: Revit API (ExternalCommand)Category: MEP | Plumbing | Thermal Analysis

🧠 What Is It?

The Hot Water Return Analyzer is a Revit add-in that analyzes the domestic hot water (DHW) system in a BIM model to:

🚨 Identify the critical run (path of maximum heat loss)

🧮 Calculate the minimum required return GPM needed to offset that loss

🔴 Visually highlight the critical heat loss path in the Revit model

📄 Export detailed CSV reports with per-pipe heat loss and total GPM result

This tool requires no flow data—it works purely from thermodynamics, pipe geometry, and insulation values, using first principles to drive return flow sizing.

🛠️ How It Works

1. User Workflow

Select any hot water pipe in your Revit model

Run the add-in via Add-Ins → External Tools → Hot Water Return Analyzer

The tool automatically:

Traverses all physically connected DHW pipes

Calculates heat loss per segment based on insulation, length, and temperature delta

Finds the most lossy path (critical run)

Computes the required return flow rate (GPM) to maintain a target ΔT

Highlights the path in red

Saves a full CSV output to C:\RevitOutput

2. Thermal Equation Used:

Q̇ = (2πL k_eff ΔT) / ln(r₂ / r₁)
GPM = Q̇ / (ρ × cp × 60)

Where:

r₁, r₂ = pipe and insulation radii (ft)

k_eff = 1 / (R-value × 2π / ln(r₂ / r₁))

Q̇ = BTU/hr heat loss

ρ = 8.34 lb/gal (water density)

cp = 1.0 BTU/lb·°F

ΔT = allowable temp drop (e.g. 10°F)

📊 Features

🔹 Heat Loss Calculation – Uses true radial conduction equations with R-value and pipe diameter

🔹 Graph-Based Traversal – Traverses all physically connected DHW pipes using ConnectorManager

🔹 Critical Path Detection – Finds the path with highest cumulative heat loss (BTU/hr)

🔹 Visual Highlighting – Overrides pipe graphics in red for QA/QC visibility

🔹 CSV Export – Outputs pipe-level loss and summary to C:\RevitOutput\HotWater_CriticalHeatPath.csv

🔹 Memoized DFS – Avoids redundant recursion and handles large networks safely

🔹 Revit API Safety – Includes null checks, connector filtering, and model-safe transactions

🧩 Installation

🔧 Prerequisites

Revit 2024 or newer

.NET Framework 4.8+

📦 Files Required

HotWaterReturnAnalyzer.dll — compiled code

HotWaterReturnAnalyzer.addin — registration manifest

🧾 .addin File Example:

<RevitAddIns>
  <AddIn Type="Command">
    <Name>Hot Water Return Analyzer</Name>
    <Assembly>"C:\Path\To\HotWaterReturnAnalyzer.dll"</Assembly>
    <AddInId>99D94F1B-9EC4-437B-AD36-1F61A62B7250</AddInId>
    <FullClassName>RevitHotWaterReturnFlow.CalculateHotWaterReturn_GPMfromCriticalLoss</FullClassName>
    <VendorId>DEUB</VendorId>
    <VendorDescription>Derek Eubanks, PE | GT MSCS</VendorDescription>
  </AddIn>
</RevitAddIns>

🧪 Example Use Case

You model a DHW loop for a hospital with hundreds of feet of insulated piping. You want to:

Determine actual return GPM needed without guesswork

Visually confirm the critical run across risers and mains

Export data for mechanical QA/QC or design documentation

This tool gives you all that—directly from the Revit model.

👨‍💻 About the Author

Derek Eubanks, PE | Mechanical Engineer | Building Automation Developer | OMSCS Candidate – Georgia Tech | Passionate about embedding real engineering logic into BIM tools using C# and thermodynamic principles.

For more Revit C# automation tools and HVAC logic built from first principles, follow or star this repo!

