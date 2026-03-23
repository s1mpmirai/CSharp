import React, { useState } from 'react';
import {
  LayoutDashboard,
  Store,
  User,
  LogOut,
  Image as ImageIcon,
  Mic,
  Volume2,
  Save,
  Plus,
  UploadCloud,
  ShoppingBasket
} from 'lucide-react';

function Login({ onLogin }: { onLogin: () => void }) {
  return (
    <div className="flex min-h-screen w-full font-poppins relative overflow-hidden bg-stone-900">
      {/* Full background image */}
      <div 
        className="absolute inset-0 bg-cover bg-center z-0"
        style={{ backgroundImage: 'url("https://images.unsplash.com/photo-1555939594-58d7cb561ad1?ixlib=rb-4.0.3&auto=format&fit=crop&w=1920&q=80")' }}
      >
        {/* Dark overlay for the left side */}
        <div className="absolute inset-0 bg-black/40"></div>
      </div>

      {/* Left Side (60%) */}
      <div className="relative z-10 hidden lg:flex lg:w-[60%] items-center justify-center p-16">
        <h1 className="text-5xl lg:text-6xl font-serif text-white leading-tight max-w-2xl font-medium drop-shadow-xl">
          "Your Journey into Street Food Audio Guide Starts Here."
        </h1>
      </div>

      {/* Right Side (40%) */}
      <div className="relative z-10 w-full lg:w-[40%] flex items-center justify-center p-8 lg:p-12 bg-[#FDFBF7]/85 backdrop-blur-xl shadow-[-20px_0_40px_rgba(0,0,0,0.15)]">
        {/* Login Card Content */}
        <div className="w-full max-w-sm">
          
          {/* Logo & Header */}
          <div className="flex flex-col items-center mb-12">
            <div className="relative flex items-center justify-center w-16 h-16 bg-white rounded-2xl shadow-sm border border-stone-100 mb-5 text-[#FF7F50]">
              <Mic size={26} className="absolute -ml-3 -mt-2" />
              <ShoppingBasket size={22} className="absolute ml-4 mt-3 opacity-90" />
            </div>
            <h2 className="font-serif text-[22px] text-stone-500 uppercase tracking-[0.25em] font-semibold">
              Portal
            </h2>
          </div>

          {/* Form */}
          <form className="space-y-8" onSubmit={(e) => { e.preventDefault(); onLogin(); }}>
            <div className="space-y-6">
              <div className="relative">
                <input 
                  type="text" 
                  id="username"
                  placeholder="Username or Email"
                  className="w-full bg-transparent border-b border-stone-300 py-2 px-0 text-stone-800 placeholder-stone-400 focus:outline-none focus:border-[#FF7F50] transition-colors peer"
                  required
                />
              </div>
              <div className="relative">
                <input 
                  type="password" 
                  id="password"
                  placeholder="Password"
                  className="w-full bg-transparent border-b border-stone-300 py-2 px-0 text-stone-800 placeholder-stone-400 focus:outline-none focus:border-[#FF7F50] transition-colors peer"
                  required
                />
              </div>
            </div>

            <div className="flex items-center justify-between mt-6">
              <label className="flex items-center gap-2 cursor-pointer group">
                <div className="relative flex items-center justify-center w-4 h-4 border border-stone-400 rounded-sm group-hover:border-[#FF7F50] transition-colors">
                  <input type="checkbox" className="peer absolute opacity-0 w-full h-full cursor-pointer" />
                  <div className="hidden peer-checked:block w-2.5 h-2.5 bg-[#FF7F50] rounded-[1px]"></div>
                </div>
                <span className="text-sm text-stone-600 group-hover:text-stone-800 transition-colors">Remember Me</span>
              </label>
              
              <a href="#" className="text-sm text-stone-500 hover:text-[#FF7F50] transition-colors">
                Forgot Password?
              </a>
            </div>

            <button 
              type="submit"
              className="w-full bg-[#FF7F50] hover:bg-[#FF6330] text-white font-medium py-3.5 rounded-xl mt-8 shadow-lg shadow-[#FF7F50]/30 transition-all active:scale-[0.98]"
            >
              Log In
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}

function Dashboard({ onLogout }: { onLogout: () => void }) {
  const [description, setDescription] = useState(
    "This stall has [Enter detailed description of your signature dishes, your stall's story, or special offers... You can freely edit this part.]"
  );
  const [voice, setVoice] = useState('Female');
  const [speed, setSpeed] = useState('Normal');

  const handleQuickInsert = () => {
    setDescription((prev) => "This stall has... " + prev);
  };

  return (
    <div className="min-h-screen bg-stone-50 flex font-sans text-stone-800">
      {/* Sidebar Navigation */}
      <aside className="w-64 bg-white border-r border-stone-200 flex flex-col fixed h-full">
        <div className="p-6 border-b border-stone-100">
          <h1 className="text-xl font-bold text-orange-600 tracking-tight">Food Street</h1>
          <p className="text-xs text-stone-500 mt-1 uppercase tracking-wider font-semibold">Audio Guide</p>
        </div>
        
        <nav className="flex-1 p-4 space-y-2">
          <a href="#" className="flex items-center gap-3 px-4 py-3 text-stone-600 hover:bg-stone-50 rounded-xl transition-colors">
            <LayoutDashboard size={20} />
            <span className="font-medium">Overview</span>
          </a>
          <a href="#" className="flex items-center gap-3 px-4 py-3 bg-orange-50 text-orange-600 rounded-xl transition-colors">
            <Store size={20} />
            <span className="font-medium">Stall Management</span>
          </a>
          <a href="#" className="flex items-center gap-3 px-4 py-3 text-stone-600 hover:bg-stone-50 rounded-xl transition-colors">
            <User size={20} />
            <span className="font-medium">My Profile</span>
          </a>
        </nav>

        <div className="p-4 border-t border-stone-100">
          <button onClick={onLogout} className="w-full flex items-center gap-3 px-4 py-3 text-stone-500 hover:text-red-600 hover:bg-red-50 rounded-xl transition-colors">
            <LogOut size={20} />
            <span className="font-medium">Logout</span>
          </button>
        </div>
      </aside>

      {/* Main Content Area */}
      <main className="flex-1 ml-64 p-8 lg:p-12 max-w-5xl">
        <header className="mb-10">
          <h2 className="text-3xl font-bold text-stone-900 tracking-tight">Manage Your Stall Information</h2>
          <p className="text-stone-500 mt-2">Update your stall details and configure the audio guide for visitors.</p>
        </header>

        <div className="space-y-8">
          {/* Card 1: Basic Stall Information */}
          <section className="bg-white p-8 rounded-2xl shadow-sm border border-stone-100">
            <h3 className="text-lg font-semibold text-stone-800 mb-6 flex items-center gap-2">
              <Store className="text-orange-500" size={20} />
              Basic Stall Information
            </h3>
            
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div className="space-y-4">
                <div>
                  <label className="block text-sm font-medium text-stone-700 mb-1.5">Stall Name</label>
                  <input 
                    type="text" 
                    placeholder="e.g., Mama's Spicy Noodles" 
                    className="w-full px-4 py-2.5 bg-stone-50 border border-stone-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 transition-all"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-stone-700 mb-1.5">Cuisine Type</label>
                  <input 
                    type="text" 
                    placeholder="e.g., Thai Street Food" 
                    className="w-full px-4 py-2.5 bg-stone-50 border border-stone-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 transition-all"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-stone-700 mb-1.5">Location / Lot Number</label>
                  <input 
                    type="text" 
                    placeholder="e.g., Zone A, Stall 12" 
                    className="w-full px-4 py-2.5 bg-stone-50 border border-stone-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 transition-all"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-stone-700 mb-1.5">Short Description</label>
                  <input 
                    type="text" 
                    placeholder="A brief catchy phrase for your stall" 
                    className="w-full px-4 py-2.5 bg-stone-50 border border-stone-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 transition-all"
                  />
                </div>
              </div>

              {/* Upload Stall Image */}
              <div className="flex flex-col">
                <label className="block text-sm font-medium text-stone-700 mb-1.5">Upload Stall Image</label>
                <div className="flex-1 border-2 border-dashed border-stone-200 rounded-xl bg-stone-50 flex flex-col items-center justify-center p-6 hover:bg-stone-100 hover:border-orange-300 transition-colors cursor-pointer group">
                  <div className="w-12 h-12 bg-white rounded-full shadow-sm flex items-center justify-center mb-4 group-hover:scale-110 transition-transform">
                    <UploadCloud className="text-orange-500" size={24} />
                  </div>
                  <p className="text-sm font-medium text-stone-700">Click to upload or drag and drop</p>
                  <p className="text-xs text-stone-500 mt-1">SVG, PNG, JPG or GIF (max. 5MB)</p>
                </div>
              </div>
            </div>
          </section>

          {/* Card 2: Detailed Description & Audio Guide */}
          <section className="bg-white p-8 rounded-2xl shadow-sm border border-stone-100">
            <div className="flex items-center justify-between mb-6">
              <h3 className="text-lg font-semibold text-stone-800 flex items-center gap-2">
                <Mic className="text-orange-500" size={20} />
                Detailed Description & Audio Guide
              </h3>
              <button 
                onClick={handleQuickInsert}
                className="flex items-center gap-1.5 text-sm font-medium text-orange-600 bg-orange-50 hover:bg-orange-100 px-3 py-1.5 rounded-lg transition-colors"
              >
                <Plus size={16} />
                Quick Insert "This stall has..."
              </button>
            </div>

            <div className="space-y-6">
              <div>
                <label className="block text-sm font-medium text-stone-700 mb-1.5">
                  Detailed Stall Description (Used for Text-to-Speech Audio)
                </label>
                <textarea
                  rows={6}
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  className="w-full px-4 py-3 bg-stone-50 border border-stone-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 transition-all resize-none leading-relaxed"
                />
              </div>


              {/* Action Buttons */}
              <div className="flex items-center justify-end gap-4 pt-4 border-t border-stone-100">
                <button className="flex items-center gap-2 px-6 py-2.5 text-stone-600 bg-stone-100 hover:bg-stone-200 font-medium rounded-xl transition-colors">
                  <Volume2 size={18} />
                  Preview Audio
                </button>
                <button className="flex items-center gap-2 px-6 py-2.5 text-white bg-orange-500 hover:bg-orange-600 font-medium rounded-xl shadow-sm shadow-orange-500/20 transition-all active:scale-95">
                  <Save size={18} />
                  Update Audio Guide
                </button>
              </div>
            </div>
          </section>
        </div>
      </main>
    </div>
  );
}

export default function App() {
  const [isLoggedIn, setIsLoggedIn] = useState(false);

  if (!isLoggedIn) {
    return <Login onLogin={() => setIsLoggedIn(true)} />;
  }

  return <Dashboard onLogout={() => setIsLoggedIn(false)} />;
}
