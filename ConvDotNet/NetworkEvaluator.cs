﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCL.Net;

namespace JaNet
{
    static class NetworkEvaluator
    {


        /// <summary>
        /// Run this method before evaluation, passing the TRAINING set as second argument.
        /// This will compute cumulative averages needed for inference in BatchNormConv layers, if any.
        /// </summary>
        /// <param name="network"></param>
        /// <param name="dataSet"></param>
        public static void PreEvaluateNetwork(NeuralNetwork network, DataSet dataSet)
        {
            // Set network for pre-inference (needed for BatchNorm layers)
            network.Set("PreInference", true);

            // Turn off dropout
            network.Set("DropoutFC", 1.0);
            network.Set("DropoutConv", 1.0);
            network.Set("DropoutInput", 1.0);

            int miniBatchSize = network.Layers[0].OutputNeurons.MiniBatchSize;
            
            Sequence indicesSequence = new Sequence(dataSet.Size);

            // Run over mini-batches (in order, no shuffling)
            for (int iStartMiniBatch = 0; iStartMiniBatch < dataSet.Size; iStartMiniBatch += miniBatchSize)  
            {
                // Feed a mini-batch to the network
                int[] miniBatch = indicesSequence.GetMiniBatchIndices(iStartMiniBatch, miniBatchSize);
                network.InputLayer.FeedData(dataSet, miniBatch);

                // Run network forward
                network.ForwardPass("beginning", "end");

                // Do not compute loss or error
                
            }
        }

        public static void EvaluateNetwork(NeuralNetwork network, DataSet dataSet, out double loss, out double error)
        {
            // Set network for inference (needed for BatchNorm layers)
            network.Set("Inference", true);

            loss = 0.0;
            error = 0.0;

            // Turn off dropout
            network.Set("DropoutFC", 1.0);
            network.Set("DropoutConv", 1.0);
            network.Set("DropoutInput", 1.0);

            int miniBatchSize = network.Layers[0].OutputNeurons.MiniBatchSize;
            
            Sequence indicesSequence = new Sequence(dataSet.Size);

            // Run over mini-batches (in order, no shuffling here)
            for (int iStartMiniBatch = 0; iStartMiniBatch < dataSet.Size; iStartMiniBatch += miniBatchSize)  
            {
                // Feed a mini-batch to the network
                int[] miniBatch = indicesSequence.GetMiniBatchIndices(iStartMiniBatch, miniBatchSize);
                network.InputLayer.FeedData(dataSet, miniBatch);

                // Run network forward
                network.ForwardPass("beginning", "end");


                for (int m = 0; m < Math.Min(miniBatchSize,dataSet.Size-iStartMiniBatch) ; m++) // In case dataSet.Size doesn't divide miniBatchSize, the last miniBatch contains copies! Don't want to re-evaluate them
                {
                    double[] outputScores = network.OutputLayer.OutputClassScores[m];

                    int assignedLabel = Utils.IndexOfMax(outputScores);
                    int trueLabel = dataSet.Labels[miniBatch[m]];

                    // Cumulate loss and error
                    loss -= Math.Log(outputScores[trueLabel]);
                    error += (assignedLabel == trueLabel) ? 0 : 1;

                } // end loop within a mini-batch
                
            } // end loop over mini-batches
             
            error /= dataSet.Size;
            loss /= dataSet.Size;
        }


        public static void ComputeBatchLossError(NeuralNetwork network, DataSet dataSet, int[] miniBatch, out double loss, out double error)
        {
            loss = 0.0;
            error = 0.0;

            // Find maximum output score (i.e. assigned class) of each mini batch item
            for (int m = 0; m < miniBatch.Length; m++)
            {
                double[] outputScores = network.OutputLayer.OutputClassScores[m];

                int assignedLabel = Utils.IndexOfMax(outputScores);
                int trueLabel = dataSet.Labels[miniBatch[m]];

                // Cumulate loss and error
                loss -= Math.Log(outputScores[trueLabel]);
                error += (assignedLabel == trueLabel) ? 0 : 1;

            } // end loop within a mini-batch

            error /= miniBatch.Length;
            loss /= miniBatch.Length;
        }


        public static void SaveMisclassifiedExamples(NeuralNetwork network, DataSet dataSet, string outputFilePath)
        {
            List<int> misclassifiedExamplesList = new List<int>();

            // Set network for inference (needed for BatchNorm layers)
            network.Set("Inference", true);

            // Turn off dropout
            network.Set("DropoutFC", 1.0);
            network.Set("DropoutConv", 1.0);
            network.Set("DropoutInput", 1.0);

            int miniBatchSize = network.Layers[0].OutputNeurons.MiniBatchSize;

            Sequence indicesSequence = new Sequence(dataSet.Size);

            // Run over mini-batches (in order, no shuffling here)
            for (int iStartMiniBatch = 0; iStartMiniBatch < dataSet.Size; iStartMiniBatch += miniBatchSize)
            {
                // Feed a mini-batch to the network
                int[] miniBatch = indicesSequence.GetMiniBatchIndices(iStartMiniBatch, miniBatchSize);
                network.InputLayer.FeedData(dataSet, miniBatch);

                // Run network forward
                network.ForwardPass("beginning", "end");

                for (int m = 0; m < Math.Min(miniBatchSize, dataSet.Size - iStartMiniBatch); m++) // In case dataSet.Size doesn't divide miniBatchSize, the last miniBatch contains copies! Don't want to re-evaluate them
                {
                    double[] outputScores = network.OutputLayer.OutputClassScores[m];

                    int assignedLabel = Utils.IndexOfMax(outputScores);
                    int trueLabel = dataSet.Labels[miniBatch[m]];

                    if (assignedLabel != trueLabel)
                        misclassifiedExamplesList.Add(miniBatch[m]);

                } // end loop within a mini-batch

            } // end loop over mini-batches

            // Save the list to file
            using (System.IO.StreamWriter outputFile = new System.IO.StreamWriter(outputFilePath))
            {
                foreach (float misclassifiedExample in misclassifiedExamplesList)
                {
                    outputFile.WriteLine(misclassifiedExample.ToString());
                }
                Console.WriteLine("Misclassified examples saved in file " + outputFilePath);
            }

        }


    }
}