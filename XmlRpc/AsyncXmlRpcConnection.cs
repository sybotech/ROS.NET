﻿// File: AsyncXmlRpcConnection.cs
// Project: XmlRpc_Wrapper
// 
// ROS.NET
// Eric McCann <emccann@cs.uml.edu>
// UMass Lowell Robotics Laboratory
// 
// Reimplementation of the ROS (ros.org) ros_cpp client in C#.
// 
// Created: 11/06/2013
// Updated: 07/23/2014

namespace XmlRpc
{
    public abstract class AsyncXmlRpcConnection
    {
        public abstract void addToDispatch(XmlRpcDispatch disp);

        public abstract void removeFromDispatch(XmlRpcDispatch disp);

        public abstract bool check();
    }
}