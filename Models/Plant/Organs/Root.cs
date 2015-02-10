using System;
using System.Collections.Generic;
using System.Text;
using Models.Core;
using Models.PMF.Functions;
using Models.Soils;
using System.Xml.Serialization;
using Models.PMF.Interfaces;
using Models.Soils.Arbitrator;

namespace Models.PMF.Organs
{
    /// <summary>
    /// The root organ
    /// </summary>
    [Serializable]
    public class Root : BaseOrgan, BelowGround
    {
        #region Links
        /// <summary>The plant</summary>
        [Link]
        Plant Plant = null;

        /// <summary>The arbitrator</summary>
        [Link]
        OrganArbitrator Arbitrator = null;

        /// <summary>The soil</summary>
        [Link]
        Soils.Soil Soil = null;
        #endregion
        
        #region Parameters
        /// <summary>Gets or sets the initial dm.</summary>
        /// <value>The initial dm.</value>
        public double InitialDM { get; set; }
        /// <summary>Gets or sets the length of the specific root.</summary>
        /// <value>The length of the specific root.</value>
        public double SpecificRootLength { get; set; }
        /// <summary>Gets or sets the kn o3.</summary>
        /// <value>The kn o3.</value>
        public double KNO3 { get; set; }
        /// <summary>Gets or sets the kn h4.</summary>
        /// <value>The kn h4.</value>
        public double KNH4 { get; set; }

        /// <summary>The nitrogen demand switch</summary>
        [Link]
        IFunction NitrogenDemandSwitch = null;
        /// <summary>The senescence rate</summary>
        [Link(IsOptional = true)]
        IFunction SenescenceRate = null;
        /// <summary>The temperature effect</summary>
        [Link]
        IFunction TemperatureEffect = null;
        /// <summary>The root front velocity</summary>
        [Link]
        IFunction RootFrontVelocity = null;
        /// <summary>The partition fraction</summary>
        [Link]
        IFunction PartitionFraction = null;
        /// <summary>The maximum n conc</summary>
        [Link]
        IFunction MaximumNConc = null;
        /// <summary>The maximum daily n uptake</summary>
        [Link]
        IFunction MaxDailyNUptake = null;
        /// <summary>The minimum n conc</summary>
        [Link]
        IFunction MinimumNConc = null;
        /// <summary>The kl modifier</summary>
        [Link]
        IFunction KLModifier = null;
        #endregion

        #region States
        /// <summary>The kgha2gsm</summary>
        private const double kgha2gsm = 0.1;
        /// <summary>The sw supply</summary>
        private double[] SWSupply = null;
        /// <summary>The uptake</summary>
        private double[] Uptake = null;
        /// <summary>The delta n h4</summary>
        private double[] DeltaNH4;
        /// <summary>The delta n o3</summary>
        private double[] DeltaNO3;
        /// <summary>The _ senescence rate</summary>
        private double _SenescenceRate = 0;
        /// <summary>The _ nuptake</summary>
        private double _Nuptake = 0;

        /// <summary>Gets or sets the layer live.</summary>
        /// <value>The layer live.</value>
        [XmlIgnore]
        public Biomass[] LayerLive { get; set; }
        /// <summary>Gets or sets the layer dead.</summary>
        /// <value>The layer dead.</value>
        [XmlIgnore]
        public Biomass[] LayerDead { get; set; }
        /// <summary>Gets or sets the length.</summary>
        /// <value>The length.</value>
        [XmlIgnore]
        public double Length { get; set; }

        /// <summary>Gets or sets the depth.</summary>
        /// <value>The depth.</value>
        [XmlIgnore]
        [Units("mm")]
        public double Depth { get; set; }

        /// <summary>Clears this instance.</summary>
        protected override void Clear()
        {
            base.Clear();
            SWSupply = null;
            Uptake = null;
            DeltaNH4 = null;
            DeltaNO3 = null;
            _SenescenceRate = 0;
            _Nuptake = 0;
            Length = 0;
            Depth = 0;

            if (LayerLive == null || LayerLive.Length == 0)
            {
                LayerLive = new Biomass[Soil.Thickness.Length];
                LayerDead = new Biomass[Soil.Thickness.Length];
                for (int i = 0; i < Soil.Thickness.Length; i++)
                {
                    LayerLive[i] = new Biomass();
                    LayerDead[i] = new Biomass();
                }
            }
            else
            {
                for (int i = 0; i < Soil.Thickness.Length; i++)
                {
                    LayerLive[i].Clear();
                    LayerDead[i].Clear();
                }
            }


            DeltaNO3 = new double[Soil.Thickness.Length];
            DeltaNH4 = new double[Soil.Thickness.Length];
        }

        #endregion
        
        #region Class Properties
        /// <summary>Gets a value indicating whether this instance is growing.</summary>
        /// <value>
        /// <c>true</c> if this instance is growing; otherwise, <c>false</c>.
        /// </value>
        private bool isGrowing { get { return (Plant.PlantInGround && Plant.SowingData.Depth < this.Depth); } }

        /// <summary>The soil crop</summary>
        private SoilCrop soilCrop;

        /// <summary>Gets the n uptake.</summary>
        /// <value>The n uptake.</value>
        [Units("kg/ha")]
        public double NUptake
        {
            get
            {
                return _Nuptake / kgha2gsm;
            }
        }

        /// <summary>Gets the l ldep.</summary>
        /// <value>The l ldep.</value>
        [Units("mm")]
        double[] LLdep
        {
            get
            {
                double[] value = new double[Soil.Thickness.Length];
                for (int i = 0; i < Soil.Thickness.Length; i++)
                    value[i] = soilCrop.LL[i] * Soil.Thickness[i];
                return value;
            }
        }

        /// <summary>Gets the length density.</summary>
        /// <value>The length density.</value>
        [Units("??mm/mm3")]
        double[] LengthDensity
        {
            get
            {
                double[] value = new double[Soil.Thickness.Length];
                for (int i = 0; i < Soil.Thickness.Length; i++)
                    value[i] = LayerLive[i].Wt * SpecificRootLength / 1000000 / Soil.Thickness[i];
                return value;
            }
        }
        /// <summary>Gets the RLV.</summary>
        /// <value>The RLV.</value>
        [Units("??km/mm3")]
        double[] rlv
        {
            get
            {
                return LengthDensity;
            }
        }
        #endregion

        #region Functions
        /// <summary>Does the potential dm.</summary>
        public override void DoPotentialDM()
        {
            _SenescenceRate = 0;
            if (SenescenceRate != null) //Default of zero means no senescence
                _SenescenceRate = SenescenceRate.Value;

          /*  if (Live.Wt == 0)
            {
                //determine how many layers to put initial DM into.
                Depth = Plant.SowingData.Depth;
                double AccumulatedDepth = 0;
                double InitialLayers = 0;
                for (int layer = 0; layer < Soil.Thickness.Length; layer++)
                {
                    if (AccumulatedDepth < Depth)
                        InitialLayers += 1;
                    AccumulatedDepth += Soil.SoilWater.Thickness[layer];
                }
                for (int layer = 0; layer < Soil.Thickness.Length; layer++)
                {
                    if (layer <= InitialLayers - 1)
                    {
                        //dirstibute root biomass evently through root depth
                        LayerLive[layer].StructuralWt = InitialDM / InitialLayers * Plant.Population;
                        LayerLive[layer].StructuralN = InitialDM / InitialLayers * MaxNconc * Plant.Population;
                    }
                }
               
            }
            */
            Length = 0;
            for (int layer = 0; layer < Soil.Thickness.Length; layer++)
                Length += LengthDensity[layer];
        }

        /// <summary>Does the actual growth.</summary>
        public override void DoActualGrowth()
        {
            base.DoActualGrowth();

            // Do Root Front Advance
            int RootLayer = LayerIndex(Depth);
            double TEM = (TemperatureEffect == null) ? 1 : TemperatureEffect.Value;

            Depth = Depth + RootFrontVelocity.Value * soilCrop.XF[RootLayer] * TEM;
            double MaxDepth = 0;
            for (int i = 0; i < Soil.Thickness.Length; i++)
                if (soilCrop.XF[i] > 0)
                    MaxDepth += Soil.Thickness[i];

            Depth = Math.Min(Depth, MaxDepth);

            // Do Root Senescence
            FOMLayerLayerType[] FOMLayers = new FOMLayerLayerType[Soil.Thickness.Length];

            for (int layer = 0; layer < Soil.Thickness.Length; layer++)
            {
                double DM = LayerLive[layer].Wt * _SenescenceRate * 10.0;
                double N = LayerLive[layer].StructuralN * _SenescenceRate * 10.0;
                LayerLive[layer].StructuralWt *= (1.0 - _SenescenceRate);
                LayerLive[layer].NonStructuralWt *= (1.0 - _SenescenceRate);
                LayerLive[layer].StructuralN *= (1.0 - _SenescenceRate);
                LayerLive[layer].NonStructuralN *= (1.0 - _SenescenceRate);



                FOMType fom = new FOMType();
                fom.amount = (float)DM;
                fom.N = (float)N;
                fom.C = (float)(0.40 * DM);
                fom.P = 0;
                fom.AshAlk = 0;

                FOMLayerLayerType Layer = new FOMLayerLayerType();
                Layer.FOM = fom;
                Layer.CNR = 0;
                Layer.LabileP = 0;

                FOMLayers[layer] = Layer;
            }
            FOMLayerType FomLayer = new FOMLayerType();
            FomLayer.Type = Plant.CropType;
            FomLayer.Layer = FOMLayers;
            IncorpFOM.Invoke(FomLayer);

            UpdateRootProperties();
        }

        /// <summary>Does the water uptake.</summary>
        /// <param name="Amount">The amount.</param>
        public override void DoWaterUptake(double[] Amount)
        {
            // Send the delta water back to SoilWat that we're going to uptake.
            WaterChangedType WaterUptake = new WaterChangedType();
            WaterUptake.DeltaWater = Utility.Math.Multiply_Value(Amount, -1.0);

            Uptake = WaterUptake.DeltaWater;
            if (WaterChanged != null)
                WaterChanged.Invoke(WaterUptake);
        }
        /// <summary>Layers the index.</summary>
        /// <param name="depth">The depth.</param>
        /// <returns></returns>
        /// <exception cref="System.Exception">Depth deeper than bottom of soil profile</exception>
        private int LayerIndex(double depth)
        {
            double CumDepth = 0;
            for (int i = 0; i < Soil.Thickness.Length; i++)
            {
                CumDepth = CumDepth + Soil.Thickness[i];
                if (CumDepth >= depth) { return i; }
            }
            throw new Exception("Depth deeper than bottom of soil profile");
        }
        /// <summary>Roots the proportion.</summary>
        /// <param name="layer">The layer.</param>
        /// <param name="root_depth">The root_depth.</param>
        /// <returns></returns>
        private double RootProportion(int layer, double root_depth)
        {
            double depth_to_layer_bottom = 0;   // depth to bottom of layer (mm)
            double depth_to_layer_top = 0;      // depth to top of layer (mm)
            double depth_to_root = 0;           // depth to root in layer (mm)
            double depth_of_root_in_layer = 0;  // depth of root within layer (mm)
            // Implementation Section ----------------------------------
            for (int i = 0; i <= layer; i++)
                depth_to_layer_bottom += Soil.Thickness[i];
            depth_to_layer_top = depth_to_layer_bottom - Soil.Thickness[layer];
            depth_to_root = Math.Min(depth_to_layer_bottom, root_depth);
            depth_of_root_in_layer = Math.Max(0.0, depth_to_root - depth_to_layer_top);

            return depth_of_root_in_layer / Soil.Thickness[layer];
        }
        /// <summary>Soils the n supply.</summary>
        /// <param name="NO3Supply">The n o3 supply.</param>
        /// <param name="NH4Supply">The n h4 supply.</param>
        private void SoilNSupply(double[] NO3Supply, double[] NH4Supply)
        {
            double[] no3ppm = new double[Soil.Thickness.Length];
            double[] nh4ppm = new double[Soil.Thickness.Length];

            for (int layer = 0; layer < Soil.Thickness.Length; layer++)
            {
                if (LayerLive[layer].Wt > 0)
                {
                    double swaf = 0;
                    swaf = (Soil.Water[layer] - Soil.SoilWater.LL15mm[layer]) / (Soil.SoilWater.DULmm[layer] - Soil.SoilWater.LL15mm[layer]);
                    swaf = Math.Max(0.0, Math.Min(swaf, 1.0));
                    no3ppm[layer] = Soil.NO3N[layer] * (100.0 / (Soil.BD[layer] * Soil.Thickness[layer]));
                    NO3Supply[layer] = Soil.NO3N[layer] * KNO3 * no3ppm[layer] * swaf;
                    nh4ppm[layer] = Soil.NH4N[layer] * (100.0 / (Soil.BD[layer] * Soil.Thickness[layer]));
                    NH4Supply[layer] = Soil.NH4N[layer] * KNH4 * nh4ppm[layer] * swaf;
                }
                else
                {
                    NO3Supply[layer] = 0;
                    NH4Supply[layer] = 0;
                }
            }
        }
        /// <summary>Updates the root properties.</summary>
        public void UpdateRootProperties()
        {
            //Plant.RootProperties.KL = Soil.KL(Plant.CropType);
            //Plant.RootProperties.LowerLimitDep = Soil.LL(Plant.CropType);
            Plant.RootProperties.RootDepth = Depth;
            Plant.RootProperties.MaximumDailyNUptake = MaxDailyNUptake.Value;
            
            double[] RLD = new double[Soil.Thickness.Length];
                for (int i = 0; i < Soil.Thickness.Length; i++)
                    RLD[i] = LayerLive[i].Wt * SpecificRootLength / 1000000 / Soil.Thickness[i];
            Plant.RootProperties.RootLengthDensityByVolume = RLD;
            
            double[] RootProp = new double[Soil.Thickness.Length];
                 for (int i = 0; i < Soil.Thickness.Length; i++)
                    RootProp[i] =  RootProportion(i, Depth);
            Plant.RootProperties.RootExplorationByLayer = RootProp;

            double[] KlAdjusted = new double[Soil.Thickness.Length];
            for (int i = 0; i < Soil.Thickness.Length; i++)
                KlAdjusted[i] = soilCrop.KL[i] * KLModifier.Value;
            Plant.RootProperties.KL = KlAdjusted;

            double[] LL_dep = new double[Soil.Thickness.Length];
            for (int i = 0; i < Soil.Thickness.Length; i++)
                LL_dep[i] = soilCrop.LL[i] * Soil.Thickness[i];
            Plant.RootProperties.LowerLimitDep = LL_dep;
   
        }
        /// <summary>Called when [simulation commencing].</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        /// <exception cref="ApsimXException">Cannot find a soil crop parameterisation for  + Name</exception>
        [EventSubscribe("Commencing")]
        private void OnSimulationCommencing(object sender, EventArgs e)
        {
            soilCrop = this.Soil.Crop(this.Plant.Name) as SoilCrop;
            if (soilCrop == null)
                throw new ApsimXException(this, "Cannot find a soil crop parameterisation for " + Name);
            Clear();
        }

        /// <summary>Called when [end crop].</summary>
        public override void OnEndCrop()
        {
            FOMLayerLayerType[] FOMLayers = new FOMLayerLayerType[Soil.Thickness.Length];

            for (int layer = 0; layer < Soil.Thickness.Length; layer++)
            {
                double DM = (LayerLive[layer].Wt + LayerDead[layer].Wt) * 10.0;
                double N = (LayerLive[layer].N + LayerDead[layer].N) * 10.0;

                FOMType fom = new FOMType();
                fom.amount = (float)DM;
                fom.N = (float)N;
                fom.C = (float)(0.40 * DM);
                fom.P = 0;
                fom.AshAlk = 0;

                FOMLayerLayerType Layer = new FOMLayerLayerType();
                Layer.FOM = fom;
                Layer.CNR = 0;
                Layer.LabileP = 0;

                FOMLayers[layer] = Layer;
            }
            FOMLayerType FomLayer = new FOMLayerType();
            FomLayer.Type = Plant.CropType;
            FomLayer.Layer = FOMLayers;
            IncorpFOM.Invoke(FomLayer);

            base.OnEndCrop();
        }

        /// <summary>Called when [sow].</summary>
        /// <param name="Sow">The sow.</param>
        public override void OnSow(SowPlant2Type Sow)
        {
            //Fixme, this can be deleted when arbitrator calculates uptake ?????
            Uptake = new double[Soil.Thickness.Length];
            
            Depth = Plant.SowingData.Depth;
            double AccumulatedDepth = 0;
            double InitialLayers = 0;
            for (int layer = 0; layer < Soil.Thickness.Length; layer++)
            {
                if (AccumulatedDepth < Depth)
                    InitialLayers += 1;
                AccumulatedDepth += Soil.SoilWater.Thickness[layer];
            }
            for (int layer = 0; layer < Soil.Thickness.Length; layer++)
            {
                if (layer <= InitialLayers - 1)
                {
                    //dirstibute root biomass evently through root depth
                    LayerLive[layer].StructuralWt = InitialDM / InitialLayers * Plant.Population;
                    LayerLive[layer].StructuralN = InitialDM / InitialLayers * MaxNconc * Plant.Population;
                }
            }
        }
        #endregion

        #region Arbitrator method calls
        /// <summary>Gets or sets the dm demand.</summary>
        /// <value>The dm demand.</value>
        public override BiomassPoolType DMDemand
        {
            get
            {
                double Demand = 0;
                if (isGrowing)
                    Demand = Arbitrator.DMSupply * PartitionFraction.Value;
                return new BiomassPoolType { Structural = Demand };
            }
        }

        /// <summary>Sets the dm potential allocation.</summary>
        /// <value>The dm potential allocation.</value>
        /// <exception cref="System.Exception">
        /// Invalid allocation of potential DM in + Name
        /// or
        /// Error trying to partition potential root biomass
        /// </exception>
        public override BiomassPoolType DMPotentialAllocation
        {
            set
            {
                if (Depth <= 0)
                    return; //cannot allocate growth where no length

                if (DMDemand.Structural == 0)
                    if (value.Structural < 0.000000000001) { }//All OK
                    else
                        throw new Exception("Invalid allocation of potential DM in" + Name);
                // Calculate Root Activity Values for water and nitrogen
                double[] RAw = new double[Soil.Thickness.Length];
                double[] RAn = new double[Soil.Thickness.Length];
                double TotalRAw = 0;
                double TotalRAn = 0; ;

                for (int layer = 0; layer < Soil.Thickness.Length; layer++)
                {
                    if (layer <= LayerIndex(Depth))
                        if (LayerLive[layer].Wt > 0)
                        {
                            RAw[layer] = Uptake[layer] / LayerLive[layer].Wt
                                       * Soil.Thickness[layer]
                                       * RootProportion(layer, Depth);
                            RAw[layer] = Math.Max(RAw[layer], 1e-20);  // Make sure small numbers to avoid lack of info for partitioning

                            RAn[layer] = (DeltaNO3[layer] + DeltaNH4[layer]) / LayerLive[layer].Wt
                                           * Soil.Thickness[layer]
                                           * RootProportion(layer, Depth);
                            RAn[layer] = Math.Max(RAw[layer], 1e-10);  // Make sure small numbers to avoid lack of info for partitioning
                        }
                        else if (layer > 0)
                        {
                            RAw[layer] = RAw[layer - 1];
                            RAn[layer] = RAn[layer - 1];
                        }
                        else
                        {
                            RAw[layer] = 0;
                            RAn[layer] = 0;
                        }
                    TotalRAw += RAw[layer];
                    TotalRAn += RAn[layer];
                }
                double allocated = 0;
                for (int layer = 0; layer < Soil.Thickness.Length; layer++)
                {
                    if (TotalRAw > 0)

                        LayerLive[layer].PotentialDMAllocation = value.Structural * RAw[layer] / TotalRAw;
                    else if (value.Structural > 0)
                        throw new Exception("Error trying to partition potential root biomass");
                    allocated += (TotalRAw > 0) ? value.Structural * RAw[layer] / TotalRAw : 0;
                }
            }
        }
        /// <summary>Sets the dm allocation.</summary>
        /// <value>The dm allocation.</value>
        /// <exception cref="System.Exception">Error trying to partition root biomass</exception>
        public override BiomassAllocationType DMAllocation
        {
            set
            {
                // Calculate Root Activity Values for water and nitrogen
                double[] RAw = new double[Soil.Thickness.Length];
                double[] RAn = new double[Soil.Thickness.Length];
                double TotalRAw = 0;
                double TotalRAn = 0;

                if (Depth <= 0)
                    return; // cannot do anything with no depth
                for (int layer = 0; layer < Soil.Thickness.Length; layer++)
                {
                    if (layer <= LayerIndex(Depth))
                        if (LayerLive[layer].Wt > 0)
                        {
                            RAw[layer] = Uptake[layer] / LayerLive[layer].Wt
                                       * Soil.Thickness[layer]
                                       * RootProportion(layer, Depth);
                            RAw[layer] = Math.Max(RAw[layer], 1e-20);  // Make sure small numbers to avoid lack of info for partitioning

                            RAn[layer] = (DeltaNO3[layer] + DeltaNH4[layer]) / LayerLive[layer].Wt
                                       * Soil.Thickness[layer]
                                       * RootProportion(layer, Depth);
                            RAn[layer] = Math.Max(RAw[layer], 1e-10);  // Make sure small numbers to avoid lack of info for partitioning

                        }
                        else if (layer > 0)
                        {
                            RAw[layer] = RAw[layer - 1];
                            RAn[layer] = RAn[layer - 1];
                        }
                        else
                        {
                            RAw[layer] = 0;
                            RAn[layer] = 0;
                        }
                    TotalRAw += RAw[layer];
                    TotalRAn += RAn[layer];
                }
                double allocated = 0;
                for (int layer = 0; layer < Soil.Thickness.Length; layer++)
                {
                    if (TotalRAw > 0)
                    {
                        LayerLive[layer].StructuralWt += value.Structural * RAw[layer] / TotalRAw;
                        allocated += value.Structural * RAw[layer] / TotalRAw;
                    }
                    else if (value.Structural > 0)
                        throw new Exception("Error trying to partition root biomass");
                        
                }
            }
        }

        /// <summary>Gets or sets the n demand.</summary>
        /// <value>The n demand.</value>
        [Units("g/m2")]
        public override BiomassPoolType NDemand
        {
            get
            {
                //Calculate N demand based on amount of N needed to bring root N content in each layer up to maximum
                double TotalDeficit = 0.0;
                double _NitrogenDemandSwitch = 1;
                if (NitrogenDemandSwitch != null) //Default of 1 means demand is always truned on!!!!
                    _NitrogenDemandSwitch = NitrogenDemandSwitch.Value;
                foreach (Biomass Layer in LayerLive)
                {
                    double NDeficit = Math.Max(0.0, MaximumNConc.Value * (Layer.Wt + Layer.PotentialDMAllocation) - Layer.N);
                    TotalDeficit += NDeficit;
                }
                TotalDeficit *= _NitrogenDemandSwitch;
                return new BiomassPoolType { Structural = TotalDeficit };
            }
        }

        /// <summary>Gets or sets the n supply.</summary>
        /// <value>The n supply.</value>
        public override BiomassSupplyType NSupply
        {
            get
            {
                if (Soil.Thickness != null)
                {
                    double[] no3supply = new double[Soil.Thickness.Length];
                    double[] nh4supply = new double[Soil.Thickness.Length];
                    SoilNSupply(no3supply, nh4supply);
                    double NSupply = (Math.Min(Utility.Math.Sum(no3supply), MaxDailyNUptake.Value) + Math.Min(Utility.Math.Sum(nh4supply), MaxDailyNUptake.Value)) * kgha2gsm;
                    return new BiomassSupplyType { Uptake = NSupply };
                }
                else
                    return new BiomassSupplyType();
            }
        }
        /// <summary>Sets the n allocation.</summary>
        /// <value>The n allocation.</value>
        /// <exception cref="System.Exception">
        /// Cannot Allocate N to roots in layers when demand is zero
        /// or
        /// Error in N Allocation:  + Name
        /// or
        /// Request for N uptake exceeds soil N supply
        /// </exception>
        public override BiomassAllocationType NAllocation
        {
            set
            {
                // Recalculate N defict following DM allocation for checking N allocation and partitioning N between layers   
                double Demand = 0.0;
                foreach (Biomass Layer in LayerLive)
                {
                    double NDeficit = Math.Max(0.0, MaximumNConc.Value * Layer.Wt - Layer.N);
                    Demand += NDeficit;
                }
                double Supply = value.Structural;
                double NAllocated = 0;
                if ((Demand == 0) && (Supply > 0.0000000001))
                { throw new Exception("Cannot Allocate N to roots in layers when demand is zero"); }

                // Allocate N to each layer
                if (Demand > 0)
                {
                    foreach (Biomass Layer in LayerLive)
                    {
                        double NDeficit = Math.Max(0.0, MaximumNConc.Value * Layer.Wt - Layer.N);
                        double fraction = NDeficit / Demand;
                        double Allocation = fraction * Supply;
                        Layer.StructuralN += Allocation;
                        NAllocated += Allocation;
                    }
                }
                if (!Utility.Math.FloatsAreEqual(NAllocated - Supply, 0.0))
                {
                    throw new Exception("Error in N Allocation: " + Name);
                }

                // uptake_gsm
                _Nuptake = value.Uptake;
                double Uptake = value.Uptake / kgha2gsm;
                NitrogenChangedType NitrogenUptake = new NitrogenChangedType();
                NitrogenUptake.Sender = "Plant2";
                NitrogenUptake.SenderType = "Plant";
                NitrogenUptake.DeltaNO3 = new double[Soil.Thickness.Length];
                NitrogenUptake.DeltaNH4 = new double[Soil.Thickness.Length];

                double[] no3supply = new double[Soil.Thickness.Length];
                double[] nh4supply = new double[Soil.Thickness.Length];
                SoilNSupply(no3supply, nh4supply);
                double NSupply = Utility.Math.Sum(no3supply) + Utility.Math.Sum(nh4supply);
                if (Uptake > 0)
                {
                    if (Uptake > NSupply + 0.001)
                        throw new Exception("Request for N uptake exceeds soil N supply");
                    double fraction = 0;
                    if (NSupply > 0) fraction = Uptake / NSupply;

                    for (int layer = 0; layer <= Soil.Thickness.Length - 1; layer++)
                    {
                        DeltaNO3[layer] = -no3supply[layer] * fraction;
                        DeltaNH4[layer] = -nh4supply[layer] * fraction;
                        NitrogenUptake.DeltaNO3[layer] = DeltaNO3[layer];
                        NitrogenUptake.DeltaNH4[layer] = DeltaNH4[layer];
                    }
                    if (NitrogenChanged != null)
                        NitrogenChanged.Invoke(NitrogenUptake);

                }

            }
        }
        /// <summary>Gets or sets the maximum nconc.</summary>
        /// <value>The maximum nconc.</value>
        public override double MaxNconc
        {
            get
            {
                return MaximumNConc.Value;
            }
        }
        /// <summary>Gets or sets the minimum nconc.</summary>
        /// <value>The minimum nconc.</value>
        public override double MinNconc
        {
            get
            {
                return MinimumNConc.Value;
            }
        }


        /// <summary>Gets or sets the water supply.</summary>
        /// <value>The water supply.</value>
        public override double[] WaterSupply(List<ZoneWaterAndN> zones)
        {
            if (zones.Count != 1)
                throw new Exception("PMF can only deal with one soil arbitrator zone at the moment");

            double[] SW = zones[0].Water;

            double[] supply = new double[Soil.Thickness.Length];
            for (int layer = 0; layer < Soil.Thickness.Length; layer++)
                if (layer <= LayerIndex(Depth))
                    supply[layer] = Math.Max(0.0, soilCrop.KL[layer] * KLModifier.Value *
                        (SW[layer] - soilCrop.LL[layer] * Soil.Thickness[layer]) * RootProportion(layer, Depth));
                else
                    supply[layer] = 0;

            return supply;
        }

        /// <summary>Gets or sets the water uptake.</summary>
        /// <value>The water uptake.</value>
        [Units("mm")]
        public override double WaterUptake
        {
            get { return Uptake == null ? 0.0 : -Utility.Math.Sum(Uptake); }
        }

        //[Units("mm")]
        //public override double WaterUptake
        //{
        //    get { return Uptake == null ? 0.0 : -Utility.Math.Sum(Uptake); }
        //}

        #endregion

        #region Event handlers
        ///// <summary>Called when [do water arbitration].</summary>
        ///// <param name="sender">The sender.</param>
        ///// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        //[EventSubscribe("DoWaterArbitration")]
        //private void OnDoWaterArbitration(object sender, EventArgs e)
        //{
        //        if (SWSupply == null || SWSupply.Length != Soil.Thickness.Length)
        //            SWSupply = new double[Soil.Thickness.Length];
        //        for (int layer = 0; layer < Soil.Thickness.Length; layer++)
        //            if (layer <= LayerIndex(Depth))
        //                SWSupply[layer] = Math.Max(0.0, soilCrop.KL[layer] * KLModifier.Value * (Soil.Water[layer] - soilCrop.LL[layer] * Soil.Thickness[layer]) * RootProportion(layer, Depth));
        //            else
        //                SWSupply[layer] = 0;

        //        WaterSupply = Utility.Math.Sum(SWSupply);
        //}

        /// <summary>Called when [water uptakes calculated].</summary>
        /// <param name="SoilWater">The soil water.</param>
        [EventSubscribe("WaterUptakesCalculated")]
        private void OnWaterUptakesCalculated(WaterUptakesCalculatedType SoilWater)
        {
        
            // Gets the water uptake for each layer as calculated by an external module (SWIM)

            Uptake = new double[Soil.Thickness.Length];

            for (int i = 0; i != SoilWater.Uptakes.Length; i++)
            {
                string UName = SoilWater.Uptakes[i].Name;
                if (UName == Plant.Name)
                {
                    int length = SoilWater.Uptakes[i].Amount.Length;
                    for (int layer = 0; layer < length; layer++)
                    {
                        Uptake[layer] = -(float)SoilWater.Uptakes[i].Amount[layer];
                    }
                }
            }
        }

        /// <summary>Occurs when [incorp fom].</summary>
        public event FOMLayerDelegate IncorpFOM;

        /// <summary>Occurs when [nitrogen changed].</summary>
        public event NitrogenChangedDelegate NitrogenChanged;

        /// <summary>Occurs when [nitrogen changed].</summary>
        public event WaterChangedDelegate WaterChanged;
        #endregion


    }
}
