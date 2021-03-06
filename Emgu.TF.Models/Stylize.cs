﻿//----------------------------------------------------------------------------
//  Copyright (C) 2004-2018 by EMGU Corporation. All rights reserved.       
//----------------------------------------------------------------------------

#if !(UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID || UNITY_STANDALONE)
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using Emgu.Models;

namespace Emgu.TF.Models
{
    public class StylizeGraph
    {
        private FileDownloadManager _downloadManager;
        private Graph _graph = null;
        private Status _status = null;

        public StylizeGraph(Status status = null)
        {
            _status = status;
            _downloadManager = new FileDownloadManager();
            
            _downloadManager.OnDownloadProgressChanged += onDownloadProgressChanged;
            _downloadManager.OnDownloadCompleted += onDownloadCompleted;
        }

        private void onDownloadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            ImportGraph();
            if (OnDownloadCompleted != null)
            {
                OnDownloadCompleted(sender, e);
            }
        }

        public event System.Net.DownloadProgressChangedEventHandler OnDownloadProgressChanged;
        public event System.ComponentModel.AsyncCompletedEventHandler OnDownloadCompleted;

        public void Init(String[] modelFiles = null, String downloadUrl = null)
        {
            _downloadManager.Clear();
            String url = downloadUrl == null ? "https://github.com/emgucv/models/raw/master/stylize_v1/" : downloadUrl;
            String[] fileNames = modelFiles == null ? new string[] { "stylize_quantized.pb" } : modelFiles;
            for (int i = 0; i < fileNames.Length; i++)
                _downloadManager.AddFile(url + fileNames[i]);
            _downloadManager.Download();
        }

        private void onDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            if (OnDownloadProgressChanged != null)
                OnDownloadProgressChanged(sender, e);
        }

        private const int NumStyles = 26;

        private static Tensor GetStyleTensor(int style, int numStyles)
        {
            float[] styleValues = new float[numStyles];
            for (int i = 0; i < numStyles; i++)
            {
                if (i == style)
                    styleValues[i] = 1.0f;
            }
            Tensor styleTensor = new Tensor(styleValues);
            return styleTensor;
        }

        private void ImportGraph()
        {
            if (_graph != null)
                _graph.Dispose();
            _graph = new Graph();
            String localFileName = _downloadManager.Files[0].LocalFile;
            byte[] model = File.ReadAllBytes(localFileName);
            if (model.Length == 0)
                throw new FileNotFoundException(String.Format("Unable to load file {0}", localFileName));
            Buffer modelBuffer = Buffer.FromString(model);

            using (ImportGraphDefOptions options = new ImportGraphDefOptions())
                _graph.ImportGraphDef(modelBuffer, options, _status);
        }

        public Tensor Stylize(Tensor imageValue, int style)
        {
            if (_graph == null)
            {
                throw new Exception("Graph is not initialized, please call Init() first;");
            }

            if (style >= NumStyles)
            {
                throw new Exception(String.Format("Style must be a number between 0 and {0}", NumStyles - 1));
            }

            Session stylizeSession = new Session(_graph);
            Tensor styleTensor = GetStyleTensor(style, NumStyles);
            
            Tensor[] finalTensor = stylizeSession.Run(
                new Output[] { _graph["input"], _graph["style_num"] }, new Tensor[] { imageValue, styleTensor },
                new Output[] { _graph[@"transformer/expand/conv3/conv/Sigmoid"] });

            return finalTensor[0];
        }

        public byte[] StylizeToJpeg(String fileName, int style)
        {
            Tensor imageTensor = Emgu.TF.Models.ImageIO.ReadTensorFromImageFile<float>(fileName, 224, 224, 128.0f, 1.0f / 128.0f);
            
            Tensor stylizedImage = Stylize(imageTensor, 0);

            return Emgu.TF.Models.ImageIO.TensorToJpeg(stylizedImage, 255.0f);
            
        }
    }
}
#endif
