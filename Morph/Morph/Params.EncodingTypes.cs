﻿using System;
using Morph.Endpoint;

namespace Morph.Params
{
    /**
     * Objects that implement IMorphInstance will automatically be sent by value.
     * This means that an instance will be encoded and sent across to the other end,
     * where it will be decoded and reinstantiated.
     * 
     * The method MorphEncode() provides a way to specify what should be sent across.
     * 
     * Use IInstanceFactory to convert ValueInstance back to an instance on the receiving end.
     * 
     * This is similar to IReferenceDecoder, but is more efficient.
     */
    public interface IMorphInstance
    {
        ValueInstance MorphEncode();
    }

    /**
     * Business objects that implement IMorphReference will automatically be sent by reference.
     * This means that a reference to the object will be sent to the other side,
     * where a proxy to the object will be created.
     * 
     * The property MorphServlet provides a shortcut to the servlet that 
     * represents this business object.  If this business object is referenced
     * from several apartments (and thus a servlet in each of those apartments)
     * then this interface may not be the best option.
     * 
     * Note: The class MorphReference is a convenience class that does most of the work.
     * 
     * On the receiving end use IReferenceFactory to create servlet proxies
     * (that represent your business objects) 
     */
    public interface IMorphReference
    {
        Servlet MorphServlet
        {
            get;
        }

        MorphApartment MorphApartment
        {
            get;
            set;
        }
    }

    /**
     * MorphReference is a common implementation of IMorphReference for local objects.
     * It is just a convenience class.
     * 
     * A MorphReference must be registered to a Morph apartment.
     * To register your business object with an apartment, assign that apartment to the property MorphApartment.
     */
    public class MorphReference : IMorphReference, IDisposable
    {
        protected MorphReference(string typeName)
        {
            _morphTypeName = typeName;
        }

        ~MorphReference()
        {
            Dispose();
        }

        #region IDisposable Members

        public virtual void Dispose()
        {
            MorphApartment = null;
        }

        #endregion

        private readonly string _morphTypeName;
        public string MorphTypeName
        {
            get => _morphTypeName;
        }

        #region IMorphReference Members

        private Servlet _morphServlet = null;
        public Servlet MorphServlet
        {
            get => _morphServlet;
        }

        private MorphApartment _morphApartment = null;
        public virtual MorphApartment MorphApartment
        {
            get => _morphApartment;
            set
            {
                if (_morphServlet != null)
                {
                    _morphApartment.Servlets.Remove(_morphServlet.ID);
                    _morphServlet = null;
                }
                _morphApartment = value;
                if (_morphApartment != null)
                    _morphServlet = _morphApartment.Servlets.Obtain(this, _morphTypeName);
            }
        }

        #endregion
    }
}