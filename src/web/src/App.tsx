import { BrowserRouter, Routes, Route } from 'react-router-dom';
import HomePage from './pages/HomePage';
import StationDetailPage from './pages/StationDetailPage';
import ReportPage from './pages/ReportPage';

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/stations/:id" element={<StationDetailPage />} />
        <Route path="/report" element={<ReportPage />} />
        <Route path="/report/:stationId" element={<ReportPage />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
