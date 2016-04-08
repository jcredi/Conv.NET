﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCL.Net;

namespace JaNet
{
    class SoftMax : Layer
    {

        #region Fields

#if OPENCL_ENABLED
        private Mem auxiliaryFloatBuffer; // needed by forward pass (TODO: check if this is REALLY needed)

        private IntPtr[] globalWorkSizePtr;
        private IntPtr[] localWorkSizePtr;
        // in this case nInput = nOutput  ==>  only need to set one global/local work size 
        // (i.e. no need to distinguish between forward and backward pass)

        private ErrorCode clError;
        private Event clEvent;
#endif

        #endregion


        #region Setup methods

        /// <summary>
        /// Constructor of Softmax layer.
        /// </summary>
        /// <param name="Beta"></param>
        public SoftMax()
        {
            this.type = "SoftMax";
        }

        /// <summary>
        ///  Connect current layer to layer given as argument.
        /// </summary>
        /// <param name="PreviousLayer"></param>
        public override void ConnectTo(Layer PreviousLayer)
        {
            base.ConnectTo(PreviousLayer);

            this.nOutputUnits = PreviousLayer.Output.NumberOfUnits;
            this.outputNeurons = new Neurons(this.nOutputUnits);

#if OPENCL_ENABLED
            this.clError = new ErrorCode();
            this.clEvent = new Event();

            SetWorkGroupSizes();

            this.auxiliaryFloatBuffer = (Mem) Cl.CreateBuffer(  OpenCLSpace.Context, 
                                                                MemFlags.ReadWrite, 
                                                                (IntPtr)sizeof(float), 
                                                                out clError);
            OpenCLSpace.CheckErr(clError, "Cl.CreateBuffer auxiliaryFloatBuffer");
#endif
        }

#if OPENCL_ENABLED
        private void SetWorkGroupSizes()
        {
            // Work group sizes will be set as follows:
            //      global work size = total number of processes needed
            //      local work size = largest divisor of global work size <= maxWorkGroupSize of device in context
            // (this is probably suboptimal, but improvements are most likely negligible compared to improvements elsewhere, e.g. in the kernels code)

            this.globalWorkSizePtr = new IntPtr[] { (IntPtr)(Output.NumberOfUnits) };
            int tmpLocalWorkSize = Output.NumberOfUnits;
            while (tmpLocalWorkSize > OpenCLSpace.MaxWorkGroupSize || tmpLocalWorkSize > OpenCLSpace.MaxWorkItemSizes[0])
                tmpLocalWorkSize /= 2;
            this.localWorkSizePtr = new IntPtr[] { (IntPtr)(tmpLocalWorkSize) };
        }
#endif

        #endregion


        #region Operating methods

        public override void FeedForward()
        {
#if OPENCL_ENABLED

            // Set kernel arguments
            clError = Cl.SetKernelArg(OpenCLSpace.SoftmaxForward, 0, Output.ActivationsGPU);
            clError |= Cl.SetKernelArg(OpenCLSpace.SoftmaxForward, 1, Input.ActivationsGPU);
            clError |= Cl.SetKernelArg(OpenCLSpace.SoftmaxForward, 2, auxiliaryFloatBuffer);
            clError |= Cl.SetKernelArg(OpenCLSpace.SoftmaxForward, 3, (IntPtr)sizeof(int), Output.NumberOfUnits);
            OpenCLSpace.CheckErr(clError, "Softmax.FeedForward(): Cl.SetKernelArg");

            // Run kernel
            clError = Cl.EnqueueNDRangeKernel( OpenCLSpace.Queue,
                                                OpenCLSpace.SoftmaxForward,
                                                1,
                                                null,
                                                globalWorkSizePtr,
                                                localWorkSizePtr,
                                                0,
                                                null,
                                                out clEvent);
            OpenCLSpace.CheckErr(clError, "Softmax.FeedForward(): Cl.EnqueueNDRangeKernel");

            clError = Cl.Finish(OpenCLSpace.Queue);
            OpenCLSpace.CheckErr(clError, "Cl.Finish");


            clError = Cl.ReleaseEvent(clEvent);
            OpenCLSpace.CheckErr(clError, "Cl.ReleaseEvent");
#else

            // use rescaling trick to improve numerical stability
            float maxInput = this.input.GetHost()[0];
            for (int i = 1; i < this.numberOfUnits; i++)
            {
                if (this.input.GetHost()[i] > maxInput)
                    maxInput = this.input.GetHost()[i];
            }

            float[] tmpOutput = new float[this.numberOfUnits];
            for (int i = 0; i < this.numberOfUnits; i++)
            {
                tmpOutput[i] = (float)Math.Exp(this.input.GetHost()[i]-maxInput);
            }
            float sum = tmpOutput.Sum();
            for (int i = 0; i < this.numberOfUnits; i++)
            {
                tmpOutput[i] /= sum;
            }

            this.output.SetHost(tmpOutput);
#endif
        }


        public override void BackPropagate()
        {
            throw new System.InvalidOperationException("Called BackPropagate() method of SoftMax layer. Don't do this! Just feed the gradient back to the previous layer!");
            // NO backprop here!!
            // Compute directly input.Delta from cross-entropy cost: faster and numerically more stable
        }

        #endregion

    }
}
