import { BrowserRouter, Routes, Route } from 'react-router-dom';
import HomePage from './pages/HomePage';
import StationDetailPage from './pages/StationDetailPage';
import ReportPage from './pages/ReportPage';
import SuburbPage from './pages/SuburbPage';
import FuelShortagePage from './pages/FuelShortagePage';

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/stations/:id" element={<StationDetailPage />} />
        <Route path="/report" element={<ReportPage />} />
        <Route path="/report/:stationId" element={<ReportPage />} />
        <Route path="/suburbs/:state/:suburb" element={<SuburbPage />} />
        <Route path="/fuel-shortage-australia" element={<FuelShortagePage />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
