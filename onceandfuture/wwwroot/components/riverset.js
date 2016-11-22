var React = require('react'); // N.B. Still need this because JSX.
import { connect } from 'react-redux'
import { refreshAllFeeds } from '../actions'
import {
  APP_BACKGROUND_COLOR,
  APP_TEXT_COLOR,
  BUTTON_STYLE,
  COLUMNSPACER,
  COLUMNWIDTH,
  RIVER_TITLE_FONT_SIZE,
  RIVER_TITLE_BACKGROUND_COLOR,
} from './style'
import RiverColumn from './rivercolumn'
import RiverProgress from './riverprogress'

const RiverSetBar = ({title, loading, load_progress, onRefresh}) => {
  const div_style = {
    backgroundColor: RIVER_TITLE_BACKGROUND_COLOR,
  };
  const head_style = {
    fontSize: RIVER_TITLE_FONT_SIZE,
    display: 'inline-block',
    paddingLeft: COLUMNSPACER,
    fontWeight: 'bold',
  };

  const refresh_color = loading ? RIVER_TITLE_BACKGROUND_COLOR : APP_TEXT_COLOR;
  const onClick = loading ? () => { } : onRefresh;

  const refresh_style = {
    display: 'inline-block',
    float: 'right',
    color: refresh_color,
  };

  return <div style={div_style}>
    <div style={head_style}>{title}</div>
    <div style={refresh_style} onClick={onClick} >
      <i style={BUTTON_STYLE} onClick={onClick} className="fa fa-refresh" />
    </div>
    <RiverProgress
      progress={load_progress / 100} 
      backgroundColor={APP_BACKGROUND_COLOR}
      />
  </div>
}

export const RiverSetBase = ({rivers, loading, load_progress, onRefresh}) => {
  const TOTAL_SPACING = COLUMNSPACER * rivers.length;
  const TOTAL_COLUMNS = COLUMNWIDTH * rivers.length;
  const TOP_BAR_HEIGHT = 33;

  const style = {
    position: 'relative',
    height: '100%',
  };
  const top_bar_style = {
    position: 'fixed',
    top: 0, left: 0, width: '100%',
    zIndex: 10,
    height: TOP_BAR_HEIGHT,
  };
  const column_set_style = {
    padding: 10,
    position: 'relative',
    width: TOTAL_SPACING + TOTAL_COLUMNS,
    height: '100%',
  };
  const column_style = {
    width: COLUMNWIDTH,
    position: 'absolute',
    top: 0,
    marginTop: TOP_BAR_HEIGHT + COLUMNSPACER,
    bottom: COLUMNSPACER,
  };
  return (
    <div style={style}>
      <div key='river_set' style={column_set_style}>
      {
        rivers.map((r, index) => {
          const c_style = Object.assign({}, column_style, {
            left: index * (COLUMNWIDTH + COLUMNSPACER) + COLUMNSPACER,
          });
          return <div style={c_style} key={r.name}>
            <RiverColumn index={index} />
          </div>
        })
      }
      </div>
      <div style={top_bar_style}>
        <RiverSetBar
          title='Rivers'
          loading={loading}
          load_progress={load_progress}
          onRefresh={onRefresh}
          />
      </div>
    </div>
  );
};

// VisibleRiverSet
//
const vrs_mapStateToProps = (state) => {
  return {
    rivers: state.rivers,
    loading: state.loading,
    load_progress: state.load_progress,
  }
};
const vrs_mapDispatchToProps = (dispatch) => {
  return {
    onRefresh: function refreshIt () { dispatch(refreshAllFeeds()); },
  };
};
const RiverSet = connect(
  vrs_mapStateToProps,
  vrs_mapDispatchToProps
)(
  RiverSetBase
);

export default RiverSet;
