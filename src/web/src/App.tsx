import { BrowserRouter, Routes, Route } from 'react-router-dom';
import HomePage from './pages/HomePage';
import StationDetailPage from './pages/StationDetailPage';

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/stations/:id" element={<StationDetailPage />} />
        <Route path="/report/:stationId" element={<div>Report — Phase 5</div>} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
